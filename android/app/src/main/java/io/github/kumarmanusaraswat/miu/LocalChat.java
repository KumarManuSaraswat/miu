package io.github.kumarmanusaraswat.miu;

import android.content.Context;
import android.os.StatFs;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.*;

/** Local text generation only. Generated text never enters the command router. */
final class LocalChat implements AutoCloseable {
    static final long MODEL_BYTES = 806058400L;
    static final String MODEL_HASH =
            "2f8c3d137f2496dcb4019e17a50f2974b89c1be1a742db3c80cc86f91cafeeef";
    static final String SYSTEM =
            "You are Miu, a helpful friendly assistant running offline. "
                    + LocalCompanion.CREATOR_REPLY
                    + " Google made the Gemma model, not the Miu application. The input is JSON:"
                    + " answer current_message directly, using recent_conversation only as past"
                    + " context. Do not repeat past replies. Give a useful,"
                    + " concise answer in plain English. You can analyze text supplied by the user."
                    + " Do not claim to access apps or perform actions.";

    interface Progress {
        void update(String text);
    }

    private final Context context;
    private final Object lock = new Object();
    private final ArrayList<JSONObject> history = new ArrayList<>();
    private Process process;
    private int epoch;
    private boolean closed, verified;

    LocalChat(Context context) {
        this.context = context.getApplicationContext();
    }

    boolean packaged() {
        if (!new File(context.getApplicationInfo().nativeLibraryDir, "libmiu_model.so").isFile())
            return false;
        try (InputStream ignored = context.getAssets().open("models/gemma3-1b.gguf")) {
            return true;
        } catch (IOException ex) {
            return false;
        }
    }

    void cancel() {
        synchronized (lock) {
            epoch++;
            if (process != null) process.destroyForcibly();
        }
    }

    void clear() {
        cancel();
        synchronized (lock) {
            history.clear();
        }
    }

    boolean running() {
        synchronized (lock) {
            return process != null && process.isAlive();
        }
    }

    private void check(int turn) throws IOException {
        synchronized (lock) {
            if (closed || turn != epoch || Thread.currentThread().isInterrupted())
                throw new IOException("Stopped.");
        }
    }

    String reply(String message, Progress progress) throws Exception {
        if (message.length() > 1200)
            throw new IOException(
                    "Please keep this question under 1,200 characters for the small offline"
                            + " model.");
        final int turn;
        final String request;
        synchronized (lock) {
            if (closed) throw new IOException("Stopped.");
            turn = ++epoch;
            request =
                    new JSONObject()
                            .put("recent_conversation", new JSONArray(history))
                            .put("current_message", escapeTokens(message))
                            .toString();
        }
        File model = prepare(turn, progress);
        check(turn);
        progress.update("Thinking offline…");
        String nativeDir = context.getApplicationInfo().nativeLibraryDir;
        ProcessBuilder builder =
                new ProcessBuilder(
                        nativeDir + "/libmiu_model.so",
                        "-m",
                        model.getAbsolutePath(),
                        "-c",
                        "2048",
                        "-n",
                        "2048",
                        "-t",
                        "4",
                        "-ngl",
                        "0",
                        "--single-turn",
                        "--simple-io",
                        "--no-display-prompt",
                        "--no-warmup",
                        "--no-escape",
                        "--color",
                        "off",
                        "--temp",
                        "0.5",
                        "--seed",
                        "42",
                        "-sys",
                        SYSTEM);
        for (String key : new ArrayList<>(builder.environment().keySet())) {
            if (key.startsWith("LLAMA_") || key.startsWith("GGML_"))
                builder.environment().remove(key);
        }
        builder.environment().put("LD_LIBRARY_PATH", nativeDir);
        builder.directory(context.getNoBackupFilesDir());
        final Process child = builder.start();
        synchronized (lock) {
            if (closed || turn != epoch) {
                child.destroyForcibly();
                throw new IOException("Stopped.");
            }
            process = child;
        }
        ScheduledExecutorService deadline = Executors.newSingleThreadScheduledExecutor();
        java.util.concurrent.atomic.AtomicBoolean timedOut =
                new java.util.concurrent.atomic.AtomicBoolean();
        deadline.schedule(
                () -> {
                    timedOut.set(true);
                    child.destroyForcibly();
                },
                90,
                TimeUnit.SECONDS);
        Thread drain =
                new Thread(
                        () -> {
                            try (InputStream errors = child.getErrorStream()) {
                                byte[] bytes = new byte[2048];
                                while (errors.read(bytes) != -1) {
                                    /* Never persist prompts or diagnostics. */
                                }
                            } catch (IOException ignored) {
                            }
                        },
                        "miu-model-diagnostics");
        drain.setDaemon(true);
        drain.start();
        try {
            try (OutputStream input = child.getOutputStream()) {
                input.write((request + "\n").getBytes(StandardCharsets.UTF_8));
            }
            StringBuilder text = new StringBuilder();
            boolean limited = false;
            try (Reader output =
                    new InputStreamReader(child.getInputStream(), StandardCharsets.UTF_8)) {
                char[] buffer = new char[512];
                int n;
                while ((n = output.read(buffer)) != -1) {
                    check(turn);
                    text.append(buffer, 0, n);
                    if (text.length() >= 800) {
                        limited = true;
                        child.destroyForcibly();
                        break;
                    }
                }
            }
            check(turn);
            if (timedOut.get())
                throw new IOException(
                        "The offline model took too long. Try a shorter question or close a large"
                                + " app.");
            if (!child.waitFor(2, TimeUnit.SECONDS) || (child.exitValue() != 0 && !limited))
                throw new IOException(
                        "The offline model could not finish. Try a shorter question or close a"
                                + " large app.");
            String answer = cleanOutput(text.toString());
            if (answer.isEmpty())
                throw new IOException("I could not form a reply. Please try again.");
            if (limited) answer = clipReply(answer);
            synchronized (lock) {
                check(turn);
                history.add(
                        new JSONObject()
                                .put("user", escapeTokens(clip(message, 300)))
                                .put("miu", escapeTokens(clip(answer, 400))));
                while (history.size() > 2) history.remove(0);
            }
            return answer;
        } finally {
            deadline.shutdownNow();
            child.destroyForcibly();
            synchronized (lock) {
                if (process == child) process = null;
            }
            drain.join(2000);
        }
    }

    private File prepare(int turn, Progress progress) throws Exception {
        File folder = new File(context.getNoBackupFilesDir(), "models");
        if (!folder.isDirectory() && !folder.mkdirs())
            throw new IOException("Miu cannot prepare its offline model storage.");
        File model = new File(folder, "gemma3-1b.gguf");
        if (verified && model.length() == MODEL_BYTES) return model;
        if (model.isFile() && model.length() == MODEL_BYTES) {
            progress.update("Checking the offline model…");
            if (verify(model, turn)) {
                verified = true;
                return model;
            }
        }
        File partial = new File(folder, "model.partial");
        if (new StatFs(folder.getAbsolutePath()).getAvailableBytes() + partial.length()
                < MODEL_BYTES + 64L * 1024 * 1024)
            throw new IOException(
                    "Free at least 900 MB of phone storage so Miu can prepare its offline model.");
        progress.update("Preparing offline AI… 0%");
        try (InputStream input = context.getAssets().open("models/gemma3-1b.gguf");
                FileOutputStream output = new FileOutputStream(partial)) {
            byte[] buffer = new byte[128 * 1024];
            long total = 0;
            int n, last = -1;
            while ((n = input.read(buffer)) != -1) {
                check(turn);
                total += n;
                if (total > MODEL_BYTES)
                    throw new IOException("The packaged model is invalid. Download Miu again.");
                output.write(buffer, 0, n);
                int percent = (int) (total * 100 / MODEL_BYTES);
                if (percent / 5 != last) {
                    last = percent / 5;
                    progress.update("Preparing offline AI… " + percent + "%");
                }
            }
            output.getFD().sync();
        }
        if (!verify(partial, turn))
            throw new IOException(
                    "The offline model failed its integrity check. Download Miu again.");
        check(turn);
        java.nio.file.Files.move(
                partial.toPath(),
                model.toPath(),
                java.nio.file.StandardCopyOption.REPLACE_EXISTING,
                java.nio.file.StandardCopyOption.ATOMIC_MOVE);
        verified = true;
        return model;
    }

    private boolean verify(File file, int turn) throws Exception {
        if (file.length() != MODEL_BYTES) return false;
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (InputStream input = new FileInputStream(file)) {
            byte[] bytes = new byte[128 * 1024];
            int n;
            while ((n = input.read(bytes)) != -1) {
                check(turn);
                digest.update(bytes, 0, n);
            }
        }
        StringBuilder hex = new StringBuilder();
        for (byte b : digest.digest()) hex.append(String.format(Locale.ROOT, "%02x", b & 255));
        return MODEL_HASH.equals(hex.toString());
    }

    static String escapeTokens(String value) {
        return value.replace("<start_of_turn>", "‹start_of_turn›")
                .replace("<end_of_turn>", "‹end_of_turn›")
                .replace("<bos>", "‹bos›")
                .replace("<eos>", "‹eos›");
    }

    static String cleanOutput(String value) {
        value =
                value.replaceAll("\u001b\\[[0-?]*[ -/]*[@-~]", "")
                        .replaceAll("[\\x00-\\x08\\x0b\\x0c\\x0e-\\x1f\\x7f]", "")
                        .trim();
        if (value.startsWith("> ")) value = value.substring(2).trim();
        if (value.endsWith("[end of text]")) value = value.substring(0, value.length() - 13).trim();
        return value;
    }

    private static String clip(String value, int length) {
        return value.length() > length ? value.substring(0, length) : value;
    }

    static String clipReply(String value) {
        value = clip(value, 800);
        int end =
                Math.max(
                        value.lastIndexOf('.'),
                        Math.max(value.lastIndexOf('!'), value.lastIndexOf('?')));
        return end >= 200 ? value.substring(0, end + 1) : value.trim() + "…";
    }

    @Override
    public void close() {
        synchronized (lock) {
            closed = true;
        }
        clear();
    }
}

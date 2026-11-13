package io.github.kumarmanusaraswat.miu;

import org.json.*;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

import javax.net.ssl.HttpsURLConnection;

final class Network {
    static final String CATALOG =
            "https://miu-preset-catalog.kumar-saraswat-s-125.workers.dev/v1/presets";

    static final class Preset {
        final String name, description, mood;
        final int rate;
        final boolean reduced;

        Preset(String n, String d, String m, int r, boolean b) {
            name = n;
            description = d;
            mood = m;
            rate = r;
            reduced = b;
        }
    }

    private static String request(String address, String key, String body, int limit)
            throws IOException {
        HttpsURLConnection connection = (HttpsURLConnection) new URL(address).openConnection();
        connection.setConnectTimeout(12000);
        connection.setReadTimeout(20000);
        connection.setInstanceFollowRedirects(false);
        connection.setRequestProperty("Accept", "application/json");
        try {
            if (body != null) {
                connection.setRequestMethod("POST");
                connection.setDoOutput(true);
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                connection.setRequestProperty("x-goog-api-key", key);
                byte[] data = body.getBytes(StandardCharsets.UTF_8);
                connection.setFixedLengthStreamingMode(data.length);
                try (OutputStream out = connection.getOutputStream()) {
                    out.write(data);
                }
            }
            int status = connection.getResponseCode();
            if (status != 200) {
                if (status == 429)
                    throw new IOException(
                            "The free quota is busy or exhausted. Try later; no paid upgrade is"
                                    + " needed.");
                if (status == 401 || status == 403)
                    throw new IOException("Check your API key and its access in Settings.");
                throw new IOException(
                        "The service is unavailable (HTTP "
                                + status
                                + "). Local commands still work.");
            }
            int declared = connection.getContentLength();
            if (declared > limit) throw new IOException("The service returned too much data.");
            try (InputStream in = connection.getInputStream();
                    ByteArrayOutputStream out = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[4096];
                int count, total = 0;
                while ((count = in.read(buffer)) != -1) {
                    total += count;
                    if (total > limit) throw new IOException("The service returned too much data.");
                    out.write(buffer, 0, count);
                }
                return out.toString(StandardCharsets.UTF_8.name());
            }
        } finally {
            connection.disconnect();
        }
    }

    static List<Preset> catalog() throws Exception {
        return parseCatalog(request(CATALOG, null, null, 65536));
    }

    static List<Preset> parseCatalog(String json) throws Exception {
        if (json.length() > 65536) throw new IOException("Invalid catalog size.");
        JSONObject root = new JSONObject(json);
        if (root.getInt("schemaVersion") != 1)
            throw new IOException("This catalog version isn’t supported yet.");
        JSONArray values = root.getJSONArray("presets");
        if (values.length() > 64) throw new IOException("Invalid catalog.");
        ArrayList<Preset> result = new ArrayList<>();
        Set<String> ids = new HashSet<>();
        for (int i = 0; i < values.length(); i++) {
            JSONObject v = values.getJSONObject(i);
            String id = v.getString("id"),
                    name = v.getString("name"),
                    description = v.getString("description"),
                    mood = v.getString("mood");
            int rate = v.getInt("speechRate");
            if (!id.matches("[a-z0-9-]{1,64}")
                    || !ids.add(id)
                    || name.trim().isEmpty()
                    || name.length() > 60
                    || description.length() > 240
                    || Expressions.resolve(mood) == null
                    || rate < -2
                    || rate > 2
                    || !(v.get("reducedMotion") instanceof Boolean))
                throw new IOException("The catalog contained an invalid preset.");
            result.add(new Preset(name, description, mood, rate, v.getBoolean("reducedMotion")));
        }
        return result;
    }

    /** Single-turn chat only. No screen, mail, notes, history, or device context is accepted. */
    static String chat(String userText, Prefs prefs) throws Exception {
        String key = prefs.readKey();
        if (key.isEmpty()) throw new IOException("Add your own Gemini key in Settings first.");
        String model = prefs.model();
        if (!model.matches("gemini-[a-z0-9.-]{1,80}"))
            throw new IOException("Check the model name in Settings.");
        JSONObject content =
                new JSONObject()
                        .put("role", "user")
                        .put(
                                "parts",
                                new JSONArray()
                                        .put(
                                                new JSONObject()
                                                        .put(
                                                                "text",
                                                                userText.substring(
                                                                        0,
                                                                        Math.min(
                                                                                2000,
                                                                                userText
                                                                                        .length())))));
        JSONObject body =
                new JSONObject()
                        .put(
                                "systemInstruction",
                                new JSONObject()
                                        .put(
                                                "parts",
                                                new JSONArray()
                                                        .put(
                                                                new JSONObject()
                                                                        .put(
                                                                                "text",
                                                                                LocalCompanion
                                                                                                .CREATOR_REPLY
                                                                                        + " You are"
                                                                                        + " Miu, a"
                                                                                        + " warm,"
                                                                                        + " concise"
                                                                                        + " pocket"
                                                                                        + " companion."
                                                                                        + " Answer"
                                                                                        + " in English"
                                                                                        + " unless"
                                                                                        + " asked"
                                                                                        + " otherwise."
                                                                                        + " You cannot"
                                                                                        + " access"
                                                                                        + " or control"
                                                                                        + " the phone,"
                                                                                        + " emails,"
                                                                                        + " apps,"
                                                                                        + " files,"
                                                                                        + " clock,"
                                                                                        + " or settings."
                                                                                        + " Never"
                                                                                        + " claim"
                                                                                        + " you performed"
                                                                                        + " an action."
                                                                                        + " For device"
                                                                                        + " actions,"
                                                                                        + " mention"
                                                                                        + " Miu's"
                                                                                        + " local"
                                                                                        + " commands"
                                                                                        + " briefly."
                                                                                        + " Do not"
                                                                                        + " include"
                                                                                        + " tool"
                                                                                        + " commands"
                                                                                        + " or markup."
                                                                                        + " Keep"
                                                                                        + " replies"
                                                                                        + " under"
                                                                                        + " 180 words."))))
                        .put("contents", new JSONArray().put(content))
                        .put("generationConfig", new JSONObject().put("maxOutputTokens", 512));
        JSONObject response =
                new JSONObject(
                        request(
                                "https://generativelanguage.googleapis.com/v1beta/models/"
                                        + model
                                        + ":generateContent",
                                key,
                                body.toString(),
                                131072));
        JSONArray candidates = response.optJSONArray("candidates");
        if (candidates == null || candidates.length() == 0)
            throw new IOException("There wasn’t a reply to show. Try rephrasing your message.");
        JSONArray parts =
                candidates.getJSONObject(0).getJSONObject("content").getJSONArray("parts");
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < parts.length(); i++)
            if (!parts.getJSONObject(i).optBoolean("thought", false))
                text.append(parts.getJSONObject(i).optString("text", ""));
        if (text.toString().trim().isEmpty()) throw new IOException("The model returned no text.");
        return text.substring(0, Math.min(6000, text.length())).trim();
    }
}

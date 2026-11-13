package io.github.kumarmanusaraswat.miu;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

final class Prefs {
    private static final String ALIAS = "miu.api-key.v1";
    final SharedPreferences values;

    Prefs(Context c) {
        values = c.getSharedPreferences("miu", Context.MODE_PRIVATE);
    }

    String mood() {
        return values.getString("mood", "idle");
    }

    boolean reduced() {
        return values.getBoolean("reduced", false);
    }

    boolean voice() {
        return values.getBoolean("voice", true);
    }

    float rate() {
        return 1f + values.getInt("rate", 0) * 0.08f;
    }

    String model() {
        return values.getString("model", "gemini-3.5-flash-lite");
    }

    private SecretKey key() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        if (!store.containsAlias(ALIAS)) {
            KeyGenerator generator =
                    KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
            generator.init(
                    new KeyGenParameterSpec.Builder(
                                    ALIAS,
                                    KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                            .build());
            generator.generateKey();
        }
        return (SecretKey) store.getKey(ALIAS, null);
    }

    void saveKey(String plain) throws Exception {
        if (plain.trim().isEmpty()) {
            if (!values.edit()
                    .remove("api_key")
                    .remove("api_iv")
                    .putBoolean("cloud", false)
                    .commit()) throw new IllegalStateException("Could not remove the key.");
            return;
        }
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.ENCRYPT_MODE, key());
        byte[] bytes = plain.trim().getBytes(StandardCharsets.UTF_8);
        try {
            String encrypted = Base64.encodeToString(cipher.doFinal(bytes), Base64.NO_WRAP);
            if (!values.edit()
                    .putString("api_key", encrypted)
                    .putString("api_iv", Base64.encodeToString(cipher.getIV(), Base64.NO_WRAP))
                    .commit()) throw new IllegalStateException("Could not save the key.");
        } finally {
            java.util.Arrays.fill(bytes, (byte) 0);
        }
    }

    String readKey() throws Exception {
        String encrypted = values.getString("api_key", "");
        if (encrypted.isEmpty()) return "";
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(
                Cipher.DECRYPT_MODE,
                key(),
                new GCMParameterSpec(
                        128, Base64.decode(values.getString("api_iv", ""), Base64.NO_WRAP)));
        byte[] bytes = cipher.doFinal(Base64.decode(encrypted, Base64.NO_WRAP));
        try {
            return new String(bytes, StandardCharsets.UTF_8);
        } finally {
            java.util.Arrays.fill(bytes, (byte) 0);
        }
    }

    boolean hasKey() {
        return !values.getString("api_key", "").isEmpty();
    }
}

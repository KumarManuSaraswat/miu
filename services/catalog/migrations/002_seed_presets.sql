-- Curated public appearance/voice preferences only. No prompts or commands.
BEGIN;
INSERT INTO miu_catalog.presets
    (id, name, description, mood, speech_rate, reduced_motion, sort_order, published)
VALUES
    ('focus', 'Focus', 'A content face, slower speech, and less motion for quiet work.', 'content', -1, true, 10, true),
    ('cheerful', 'Cheerful', 'A cheerful face with a brighter speaking pace.', 'cheerful', 1, false, 20, true),
    ('gentle', 'Gentle', 'A calm companion with relaxed speech and reduced movement.', 'content', -2, true, 30, true),
    ('playful', 'Playful', 'A playful expression and a lively speaking pace.', 'playful', 1, false, 40, true),
    ('quiet-company', 'Quiet company', 'A sleepy face and minimal movement for a quieter desktop.', 'sleepy', -1, true, 50, true),
    ('curious', 'Curious', 'An inquisitive face with a natural speaking pace.', 'curious', 0, false, 60, true)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name, description = EXCLUDED.description, mood = EXCLUDED.mood,
    speech_rate = EXCLUDED.speech_rate, reduced_motion = EXCLUDED.reduced_motion,
    sort_order = EXCLUDED.sort_order, published = EXCLUDED.published;
COMMIT;

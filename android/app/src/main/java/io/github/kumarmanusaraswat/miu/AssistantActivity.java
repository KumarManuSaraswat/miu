package io.github.kumarmanusaraswat.miu;

/** A separate, compact assistant surface. System assist-context extras are never read. */
public final class AssistantActivity extends MainActivity {
    @Override
    protected boolean isAssistantSurface() {
        return true;
    }
}

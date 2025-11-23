🎵 Sonoria – LLM Chat Module Overview
1. Purpose

The LLM Chat module provides a structured way for Sonoria (the Unity-based educational music game) to interact with large language models (LLMs).
It acts as the bridge between Unity gameplay systems and external AI reasoning, enabling symbolic interpretation, validation, and generation of musical data (e.g., scales, chord progressions).

The module powers both:

Interactive “chat” prompts (for debugging, learning feedback, etc.)

Backend interpretation for music generation tasks (scales, chords, etc.)

2. Core Components
LLMManager.cs

Central access point for sending prompts and receiving responses.

Handles session state, message formatting, and error recovery.

Routes API calls through a pluggable provider (e.g., OpenAIProvider).

Responsibilities:

Serialize prompt messages (system + user)

Handle async communication

Manage API tokens, model settings, retries, and JSON extraction

ILLMProvider.cs (Interface)

Defines the abstraction layer so different model providers (OpenAI, MusicLang, local models, etc.) can plug in seamlessly.

public interface ILLMProvider {
    Task<string> SendPromptAsync(string prompt, CancellationToken ct = default);
}


All LLM-driven classes depend on this interface rather than any specific provider, enabling model swaps without changing game logic.

OpenAIProvider.cs

Concrete implementation of ILLMProvider for ChatGPT models via the OpenAI API.
Handles:

HTTP requests and authentication

Rate limiting and token counting

Model configuration (e.g., gpt-4-turbo, temperature, etc.)

3. Design Pattern
Pipeline Integration

The LLM Chat module serves as the entry point in multi-stage pipelines like MusicOrchestrator:

Interpret user request (generate_scale "C Lydian")

Validate JSON format and symbolic data

Realize deterministic musical structures (notes, intervals)

Optionally Re-critique (TheoryCritic) for semantic correction

All LLM calls are logged with structured color-coded feedback in the Unity Console.

LLM Rationale Logging

Each LLM call can optionally return a rationale field in the JSON, e.g.:

{
  "root_pc": 0,
  "scale_kind": "lydian",
  "intervals_from_root": [0,2,4,6,7,9,11,12],
  "rationale": "Lydian has a raised 4th compared to Major; 0=C, intervals ascend to 12."
}


This allows developers and players to see the model’s reasoning, useful for debugging and educational transparency.

4. Current Usage

Used by MusicOrchestrator → InterpretScaleAsync() to decode text requests into structured InterpScale objects.

Used by TheoryCritic to generate alternative solutions when deterministic validation fails.

Will later power chord progression generation and adaptive feedback dialogs within gameplay.

5. Planned Extensions

IChordProvider: new interface for generating chord progressions via LLM or MusicLang.

LLM Critic Framework: a secondary reasoning pass for self-correction and justification.

Caching / Cost Management: minimizing duplicate API calls with local result storage.

Specialized Model Adapters: support for symbolic music models (MusicLang, MuseMorphose, etc.) through the same provider interface.

6. Design Philosophy

LLMs handle symbolic reasoning (interpretation, critique, context)

Unity handles deterministic logic (validation, playback, octave placement)

Modular and swappable architecture: any LLM or music model can be dropped in with minimal changes.

Transparent, explainable results through rationale logging.


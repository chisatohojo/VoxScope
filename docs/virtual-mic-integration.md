# Virtual microphone integration notes

VoxScope currently ends its audio pipeline at a physical output device. A future virtual microphone feature should be treated as a separate output target, not as another effect.

## Current boundary

The present pipeline is:

`capture -> effects -> analyzer -> playback`

The future target split should be:

`capture -> effects -> analyzer -> physical playback`

and optionally:

`capture -> effects -> analyzer -> virtual microphone output`

## Design decisions to settle first

- Whether VoxScope ships its own driver or integrates with an already installed virtual audio device.
- Whether physical playback, virtual microphone output, or both can be active at the same time.
- How device loss, exclusive-mode conflicts, and sample-rate mismatches are surfaced to the user.
- How installation, signing, and uninstall flows are handled if a driver is distributed.

## Implementation direction

- Keep the DSP chain output in one reusable `ISampleProvider`.
- Add an output abstraction so physical playback and future virtual microphone routing can share the same processed stream.
- Keep driver-specific code outside the effect chain and analyzer.
- Treat driver packaging and signing as release engineering work, separate from normal app publishing.

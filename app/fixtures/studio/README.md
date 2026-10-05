# Synthetic studio fixture

Five original 1440 x 900 PNGs and version-1 metadata frozen from the legacy
`scripts/rebuild_fixture.py` on 2026-09-23. This is an invented settings walkthrough,
not a desktop recording. It preserves the generated/reviewed, pending, stale/manual,
and edited/manual states used in the prototype. No user content or model files exist here.

`manifest.json` pins every installed byte. The .NET development tool embeds these
assets, verifies their hashes, and publishes a fixture directory atomically beneath
the repository's `.tmp` directory. Existing fixtures are never reset or repaired
automatically. No Python, Pillow, or installed font is needed to install the fixture.

From the repository root:

```powershell
dotnet run --project app/tools/Phraseback.Tools -- --fixture-root .tmp/rebuild/prototype-data
```

The original fixture generator remains reference-only until Python retirement.

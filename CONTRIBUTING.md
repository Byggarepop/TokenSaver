# Releasing a new version

1. Create a branch: `git checkout -b bump-X.Y.Z`
2. Update `<Version>` in `mcp/TokenSaver.Mcp.csproj`
3. Update both `"version"` fields in `.mcp/server.json` to match (the release
   workflow also re-stamps these from the tag, so this is just to keep the
   committed file honest)
4. Add a section to `CHANGELOG.md`:
   ```md
   ## [X.Y.Z] - YYYY-MM-DD

   ### Added
   - ...

   ### Fixed
   - ...
   ```
5. Squash-merge the branch to `main`
6. Tag and push:
   ```powershell
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

Pushing the tag triggers the GitHub Actions workflow which builds, packs,
publishes to NuGet (via Trusted Publishing — no stored API key), creates a
GitHub Release with the changelog section as the release body, and then
publishes the listing to the Official MCP Registry. No manual `mcp-publisher`
step is needed.

The NuGet and registry steps are opt-in via repo variables — see the header of
`.github/workflows/release.yml` for the one-time setup (`PUBLISH_NUGET`,
`PUBLISH_MCP_REGISTRY`, the nuget.org trusted-publishing policy, and the
`NUGET_USER` secret).

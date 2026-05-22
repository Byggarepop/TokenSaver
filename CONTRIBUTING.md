# Releasing a new version

1. Create a branch: `git checkout -b bump-X.Y.Z`
2. Update `<Version>` in `mcp/TokenSaver.Mcp.csproj`
3. Update both `"version"` fields in `server.json` (root) to match
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
7. Once NuGet publish completes, update the MCP Registry:
   ```
   mcp-publisher publish
   ```

Pushing the tag triggers the GitHub Actions workflow which builds, packs,
publishes to NuGet, and creates a GitHub Release with the changelog section
as the release body.

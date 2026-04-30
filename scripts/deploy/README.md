# TestFlight Deploy

This folder contains the iOS deploy script for Pocket Casebook.

## Full Deploy

### Step 1: Bump the iPhone build number

Open `ProjectSettings/ProjectSettings.asset` and increment `buildNumber.iPhone` by 1 before every deploy.

Example:

```yaml
buildNumber:
  iPhone: 6
```

TestFlight rejects duplicate or lower build numbers.

### Step 2: Commit and push your changes

From the repo root:

```powershell
cd C:\Users\blued\OneDrive\Desktop\CrimeGame
git add ProjectSettings/ProjectSettings.asset
git add <any other files you changed>
git commit -m "build: bump iOS build number to 6"
git push
```

### Step 3: Run the deploy script

From this folder:

```powershell
cd C:\Users\blued\OneDrive\Desktop\CrimeGame\scripts\deploy
node deploy.js
```

This single command:

- Triggers a new Unity Cloud Build for iOS
- Polls every 5 minutes until success
- Downloads the IPA
- Commits and pushes the IPA through Git LFS
- Fires the `upload-testflight.yml` GitHub Actions workflow

If you already have a successful Unity Cloud Build and only want to upload it:

```powershell
cd C:\Users\blued\OneDrive\Desktop\CrimeGame\scripts\deploy
node deploy.js --build <number>
```

## Monitoring

Track upload progress here:

- [GitHub Actions](https://github.com/nmotto13-code/CrimeGameRepo/actions)

After the workflow is green, the build should usually appear in TestFlight within about 5 minutes.

## Notes

- Unity version: `6000.3.8f1`
- Bundle ID: `com.pocketcase.app`
- Default branch: `master`
- Never commit `scripts/deploy/.env`

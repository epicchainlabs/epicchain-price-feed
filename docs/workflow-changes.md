# Workflow Changes

## GenerateKey.yml

Change all instances of `actions/upload-artifact@v3` to `actions/upload-artifact@v2`.

## price-feed.yml

Change all instances of `actions/upload-artifact@v3` to `actions/upload-artifact@v2`.

## Reason

There seems to be an issue with the v3 version of the upload-artifact action in the current GitHub Actions environment. Downgrading to v2 should resolve the issue.

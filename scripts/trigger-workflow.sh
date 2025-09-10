#!/bin/bash
# Trigger GitHub Actions workflow manually

REPO="r3e-network/neo-price-feed"
WORKFLOW_ID="price-feed.yml"
BRANCH="master"

echo "🚀 Triggering workflow: $WORKFLOW_ID"
echo "Repository: $REPO"
echo "Branch: $BRANCH"

if [ -z "$GITHUB_TOKEN" ]; then
    echo "❌ Error: GITHUB_TOKEN environment variable not set"
    echo "Please set it with: export GITHUB_TOKEN=your_token"
    exit 1
fi

# Trigger the workflow
response=$(curl -s -X POST \
    -H "Accept: application/vnd.github.v3+json" \
    -H "Authorization: token $GITHUB_TOKEN" \
    https://api.github.com/repos/$REPO/actions/workflows/$WORKFLOW_ID/dispatches \
    -d "{\"ref\":\"$BRANCH\"}")

if [ -z "$response" ]; then
    echo "✅ Workflow triggered successfully!"
    echo "Check status at: https://github.com/$REPO/actions"
else
    echo "❌ Error triggering workflow:"
    echo "$response"
fi
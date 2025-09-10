#!/bin/bash

# Script to set up TEE key generation for Neo Price Feed
# This script helps verify and generate the TEE key required for the price feed service

set -e

echo "🔐 Neo Price Feed TEE Key Setup"
echo "================================"

# Check if GitHub CLI is authenticated
if ! gh auth status >/dev/null 2>&1; then
    echo "❌ Error: GitHub CLI is not authenticated"
    echo "Please run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI is authenticated"

# Check current repository
REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)
echo "📁 Repository: $REPO"

# Check if REPO_ACCESS_TOKEN secret exists
echo ""
echo "🔍 Checking for REPO_ACCESS_TOKEN secret..."
if gh secret list | grep -q "REPO_ACCESS_TOKEN"; then
    echo "✅ REPO_ACCESS_TOKEN secret exists"
    HAS_TOKEN=true
else
    echo "❌ REPO_ACCESS_TOKEN secret not found"
    HAS_TOKEN=false
fi

# Check if TEE account secrets exist
echo ""
echo "🔍 Checking for existing TEE account secrets..."
SECRETS=$(gh secret list)
if echo "$SECRETS" | grep -q "NEO_TEE_ACCOUNT_ADDRESS"; then
    echo "✅ NEO_TEE_ACCOUNT_ADDRESS exists"
    TEE_EXISTS=true
elif echo "$SECRETS" | grep -q "NEO_ACCOUNT_ADDRESS"; then
    echo "✅ NEO_ACCOUNT_ADDRESS exists (legacy)"
    TEE_EXISTS=true
else
    echo "❌ No TEE account secrets found"
    TEE_EXISTS=false
fi

# Instructions based on current state
echo ""
echo "📋 Current Status:"
echo "   REPO_ACCESS_TOKEN: $([ "$HAS_TOKEN" = true ] && echo "✅ Present" || echo "❌ Missing")"
echo "   TEE Account: $([ "$TEE_EXISTS" = true ] && echo "✅ Generated" || echo "❌ Not Generated")"

if [ "$TEE_EXISTS" = true ]; then
    echo ""
    echo "🎉 TEE Key Already Exists!"
    echo "Your Neo account is already generated and stored in GitHub Secrets."
    echo ""
    echo "To verify the account details:"
    echo "1. Go to: https://github.com/$REPO/settings/secrets/actions"
    echo "2. Look for NEO_TEE_ACCOUNT_ADDRESS or NEO_ACCOUNT_ADDRESS"
    echo ""
    echo "To use the account in workflows, reference:"
    echo "  - \${{ secrets.NEO_TEE_ACCOUNT_ADDRESS }}"
    echo "  - \${{ secrets.NEO_TEE_ACCOUNT_PRIVATE_KEY }}"
    exit 0
fi

if [ "$HAS_TOKEN" = false ]; then
    echo ""
    echo "⚠️  Setup Required: REPO_ACCESS_TOKEN"
    echo "======================================"
    echo ""
    echo "To generate the TEE key, you need to create a REPO_ACCESS_TOKEN secret."
    echo "This token allows the workflow to create GitHub secrets."
    echo ""
    echo "Steps to create the token:"
    echo "1. Go to: https://github.com/settings/tokens"
    echo "2. Click 'Generate new token' → 'Generate new token (classic)'"
    echo "3. Set expiration and select these scopes:"
    echo "   ✅ repo (Full control of private repositories)"
    echo "   ✅ admin:repo_hook (Full control of repository hooks)"
    echo "4. Click 'Generate token' and copy the token"
    echo ""
    echo "Then add it as a repository secret:"
    echo "5. Go to: https://github.com/$REPO/settings/secrets/actions"
    echo "6. Click 'New repository secret'"
    echo "7. Name: REPO_ACCESS_TOKEN"
    echo "8. Value: [paste your token]"
    echo ""
    echo "After adding the token, run this script again or manually trigger:"
    echo "gh workflow run GenerateKey.yml"
    exit 1
fi

# If we have the token but no TEE key, trigger the workflow
echo ""
echo "🚀 Triggering TEE Key Generation"
echo "================================"
echo ""
echo "REPO_ACCESS_TOKEN is present. Triggering the key generation workflow..."

if gh workflow run GenerateKey.yml; then
    echo "✅ Workflow triggered successfully!"
    echo ""
    echo "⏳ Monitoring workflow progress..."
    echo "You can also monitor at: https://github.com/$REPO/actions"
    
    # Wait a moment for the workflow to start
    sleep 5
    
    # Show recent workflow runs
    echo ""
    echo "📊 Recent GenerateKey workflow runs:"
    gh run list --workflow="GenerateKey.yml" --limit=3
    
    echo ""
    echo "💡 Next steps:"
    echo "1. Wait for the workflow to complete (usually 1-2 minutes)"
    echo "2. Check the workflow logs if it fails"
    echo "3. Once successful, the TEE key will be stored in GitHub Secrets"
    echo "4. Run this script again to verify the key was created"
else
    echo "❌ Failed to trigger workflow"
    echo "Please check your permissions and try again"
    exit 1
fi
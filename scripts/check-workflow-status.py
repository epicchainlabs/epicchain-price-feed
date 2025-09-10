#!/usr/bin/env python3
"""
Check GitHub Actions workflow status for the price feed
"""

import os
import requests
import json
from datetime import datetime

# GitHub API configuration
GITHUB_API = "https://api.github.com"
REPO_OWNER = "r3e-network"
REPO_NAME = "neo-price-feed"
WORKFLOW_NAME = "price-feed.yml"

# Optional: Set GITHUB_TOKEN environment variable for higher rate limits
GITHUB_TOKEN = os.getenv("GITHUB_TOKEN")

def get_headers():
    """Get headers for GitHub API requests."""
    headers = {
        "Accept": "application/vnd.github.v3+json"
    }
    if GITHUB_TOKEN:
        headers["Authorization"] = f"token {GITHUB_TOKEN}"
    return headers

def get_workflow_runs():
    """Get recent workflow runs."""
    url = f"{GITHUB_API}/repos/{REPO_OWNER}/{REPO_NAME}/actions/workflows/{WORKFLOW_NAME}/runs"
    params = {
        "per_page": 5,
        "page": 1
    }
    
    response = requests.get(url, headers=get_headers(), params=params)
    if response.status_code != 200:
        print(f"Error fetching workflow runs: {response.status_code}")
        print(response.text)
        return None
    
    return response.json()

def get_workflow_jobs(run_id):
    """Get jobs for a specific workflow run."""
    url = f"{GITHUB_API}/repos/{REPO_OWNER}/{REPO_NAME}/actions/runs/{run_id}/jobs"
    
    response = requests.get(url, headers=get_headers())
    if response.status_code != 200:
        print(f"Error fetching jobs: {response.status_code}")
        return None
    
    return response.json()

def format_duration(start_time, end_time):
    """Format duration between two times."""
    if not start_time or not end_time:
        return "N/A"
    
    start = datetime.fromisoformat(start_time.replace('Z', '+00:00'))
    end = datetime.fromisoformat(end_time.replace('Z', '+00:00'))
    duration = end - start
    
    minutes = int(duration.total_seconds() / 60)
    seconds = int(duration.total_seconds() % 60)
    return f"{minutes}m {seconds}s"

def format_time_ago(timestamp):
    """Format how long ago a timestamp was."""
    if not timestamp:
        return "N/A"
    
    time = datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
    now = datetime.now(time.tzinfo)
    diff = now - time
    
    if diff.days > 0:
        return f"{diff.days} days ago"
    elif diff.seconds > 3600:
        hours = diff.seconds // 3600
        return f"{hours} hours ago"
    elif diff.seconds > 60:
        minutes = diff.seconds // 60
        return f"{minutes} minutes ago"
    else:
        return "just now"

def print_workflow_status():
    """Print the status of recent workflow runs."""
    print("🔍 Checking GitHub Actions workflow status...")
    print(f"Repository: {REPO_OWNER}/{REPO_NAME}")
    print(f"Workflow: {WORKFLOW_NAME}")
    print("=" * 80)
    
    # Get workflow runs
    runs_data = get_workflow_runs()
    if not runs_data:
        print("❌ Failed to fetch workflow runs")
        return
    
    runs = runs_data.get("workflow_runs", [])
    if not runs:
        print("No workflow runs found")
        return
    
    # Display each run
    for i, run in enumerate(runs):
        print(f"\n{'='*80}" if i > 0 else "")
        print(f"Run #{run['run_number']} - {run['name']}")
        print(f"Status: {get_status_emoji(run['status'])} {run['status'].upper()}")
        if run['conclusion']:
            print(f"Conclusion: {get_conclusion_emoji(run['conclusion'])} {run['conclusion'].upper()}")
        print(f"Branch: {run['head_branch']}")
        print(f"Commit: {run['head_sha'][:8]} - {run['head_commit']['message'].split('\n')[0]}")
        print(f"Started: {format_time_ago(run['created_at'])}")
        print(f"Duration: {format_duration(run['created_at'], run['updated_at'])}")
        print(f"URL: {run['html_url']}")
        
        # Get jobs for this run
        if run['status'] != 'queued':
            jobs_data = get_workflow_jobs(run['id'])
            if jobs_data:
                print("\nJobs:")
                for job in jobs_data.get('jobs', []):
                    status_emoji = get_status_emoji(job['status'])
                    conclusion_emoji = get_conclusion_emoji(job['conclusion']) if job['conclusion'] else ""
                    print(f"  - {job['name']}: {status_emoji} {job['status']}", end="")
                    if job['conclusion']:
                        print(f" ({conclusion_emoji} {job['conclusion']})", end="")
                    print(f" [{format_duration(job['started_at'], job['completed_at'])}]")
                    
                    # Show failed steps
                    if job['conclusion'] == 'failure':
                        for step in job.get('steps', []):
                            if step.get('conclusion') == 'failure':
                                print(f"    ❌ Failed step: {step['name']}")

def get_status_emoji(status):
    """Get emoji for status."""
    status_emojis = {
        'completed': '✅',
        'in_progress': '🔄',
        'queued': '⏳',
        'waiting': '⏸️',
        'requested': '📋',
        'pending': '🔸'
    }
    return status_emojis.get(status, '❓')

def get_conclusion_emoji(conclusion):
    """Get emoji for conclusion."""
    conclusion_emojis = {
        'success': '✅',
        'failure': '❌',
        'cancelled': '🚫',
        'skipped': '⏭️',
        'neutral': '➖'
    }
    return conclusion_emojis.get(conclusion, '❓')

def main():
    """Main function."""
    try:
        print_workflow_status()
        
        # Additional info
        print("\n" + "="*80)
        print("💡 Tips:")
        print("- Set GITHUB_TOKEN environment variable for higher API rate limits")
        print("- View detailed logs at: https://github.com/r3e-network/neo-price-feed/actions")
        print("- Workflow runs every 4 hours or can be triggered manually")
        
    except Exception as e:
        print(f"❌ Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()
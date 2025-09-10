# Configuring Price Feed Intervals

This document explains how to configure the interval for the price feed service.

## Overview

The price feed service runs on a schedule defined by a cron expression in the GitHub Actions workflow file. This determines how often price data is fetched and submitted to the blockchain.

## Configuring the Schedule

To change how often the price feed service runs:

1. Edit the `.github/workflows/scheduled-price-feed.yml` file
2. Locate the `schedule` section near the top of the file:
   ```yaml
   on:
     schedule:
       # Run once per week on Monday at 00:00 UTC
       - cron: '0 0 * * 1'
   ```
3. Update the cron expression to your desired schedule
4. Commit and push the changes

## Cron Expression Format

Cron expressions use the following format:
```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of the month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of the week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
│ │ │ │ │
* * * * *
```

## Common Cron Expressions

| Description | Cron Expression |
|-------------|----------------|
| Every minute | `* * * * *` |
| Every 5 minutes | `*/5 * * * *` |
| Every 10 minutes | `*/10 * * * *` |
| Every 15 minutes | `*/15 * * * *` |
| Every 30 minutes | `*/30 * * * *` |
| Every hour | `0 * * * *` |
| Every 2 hours | `0 */2 * * *` |
| Every 6 hours | `0 */6 * * *` |
| Every 12 hours | `0 */12 * * *` |
| Once a day (midnight) | `0 0 * * *` |
| Once a week (Monday at midnight) | `0 0 * * 1` |

## Recommendations

When choosing an interval, consider:

1. **Blockchain Transaction Costs**: More frequent updates mean more transactions and higher costs.
2. **Asset Volatility**: Volatile assets may need more frequent updates than stable ones.
3. **Data Freshness Requirements**: How fresh does the price data need to be for your use case?

### Recommended Intervals

- **High-Frequency Updates** (for volatile assets): Every 5-15 minutes
- **Standard Updates** (balanced approach): Every hour or daily
- **Low-Frequency Updates** (for stable assets): Daily or weekly
- **Very Low-Frequency Updates** (for extremely stable assets): Weekly or monthly

## Example Configurations

### High-Frequency Updates

```yaml
on:
  schedule:
    # Run every 2 minutes
    - cron: '*/2 * * * *'
```

### Standard Updates

```yaml
on:
  schedule:
    # Run every 10 minutes
    - cron: '*/10 * * * *'
```

### Low-Frequency Updates

```yaml
on:
  schedule:
    # Run every day at midnight
    - cron: '0 0 * * *'
```

### Very Low-Frequency Updates

```yaml
on:
  schedule:
    # Run once per week on Monday at midnight
    - cron: '0 0 * * 1'
```

## GitHub Actions Limitations

Please note that GitHub Actions has some limitations on scheduled workflows:

1. GitHub Actions schedules are not guaranteed to run exactly on time. There might be delays, especially during periods of high GitHub usage.
2. The minimum interval is 5 minutes for public repositories (1 minute for private repositories).
3. If you set a very frequent schedule, consider the GitHub Actions usage limits for your account.

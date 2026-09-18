// The tests share process-wide state: CLAUDE_CONFIG_DIR, the SystemEvents window and the
// notification area. Running them one at a time keeps them from stepping on each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

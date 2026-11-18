using System.Diagnostics;
using RustIQ.Simulator;

// Polling strategy simulator for the thesis (UK1). See backend/SIMULATOR_PLAN.md.
//
//   check-data    --data <export dir> [<export dir> ...] [--no-checksums]
//   evaluate-real --data <dir> [...] [--from <utc>] [--to <utc>] [--out <dir>]
//   run   --data <dir> [...] --strategy fourlane|uniform|priority [--budget <queries/min>]
//         [--batch-scale <x>] [--ranking frozen|hourly] [--from] [--to] [--eval-from] [--split]
//         [--seed <n>] [--validate] [priority options] --out <dir>
//   sweep --data <dir> [...] [--budgets 150 200 ...] [--strategies ...] [period options] --out <dir>
//   tune  --data <dir> [...] --budget <queries/min> [--min-intervals ...] [--prior-seconds-values ...]
//         [--wipe-boosts ...] [--ewma-alphas ...] [--objective pw-aoi|mean-aoi|mae] --out <dir>
//
// Priority options: --min-interval <s> --max-backoff <s> --wipe-boost <x> --ewma-alpha <a>
//                   --prior-changes <n> --prior-seconds <s> --score expected-age|change-probability
//                   --max-age <s> (0 = off) --no-predicted-wipes
try
{
    var commandLine = CommandLine.Parse(args);
    var stopwatch = Stopwatch.StartNew();
    void Log(string message) => Console.WriteLine($"[{stopwatch.Elapsed:mm\\:ss}] {message}");

    switch (commandLine.Command)
    {
        case "check-data":
            SimulatorCommands.CheckData(commandLine, Log);
            break;
        case "evaluate-real":
            SimulatorCommands.EvaluateReal(commandLine, Log);
            break;
        case "run":
            SimulationCommands.Run(commandLine, Log);
            break;
        case "sweep":
            SimulationCommands.Sweep(commandLine, Log);
            break;
        case "diagnose":
            SimulationCommands.Diagnose(commandLine, Log);
            break;
        case "tune":
            SimulationCommands.Tune(commandLine, Log);
            break;
        default:
            throw new ArgumentException($"Unknown command '{commandLine.Command}'.");
    }

    return 0;
}
catch (Exception exception) when (exception is ArgumentException or InvalidDataException or FileNotFoundException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

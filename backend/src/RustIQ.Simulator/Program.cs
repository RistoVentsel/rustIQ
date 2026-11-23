using System.Diagnostics;
using RustIQ.Simulator;

try
{
    CommandLine commandLine = CommandLine.Parse(args);
    Stopwatch stopwatch = Stopwatch.StartNew();

    void Log(string message)
    {
        Console.WriteLine($"[{stopwatch.Elapsed:mm\\:ss}] {message}");
    }

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
        case "trace":
            SimulationCommands.Trace(commandLine, Log);
            break;
        case "evaluate-wipes":
            RustIQ.Simulator.Wipes.WipeEvaluation.Run(commandLine, Log);
            break;
        default:
            throw new ArgumentException($"Unknown command '{commandLine.Command}'.");
    }

    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (FileNotFoundException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

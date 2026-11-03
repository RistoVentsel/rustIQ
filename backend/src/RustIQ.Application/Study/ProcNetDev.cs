namespace RustIQ.Application.Study;

public static class ProcNetDev
{
    /// <summary>
    /// Sums the received and sent byte counters of every interface in /proc/net/dev except loopback.
    /// Inside a container that file lists only the container's own interfaces, so the sums are the
    /// container's network traffic.
    /// </summary>
    public static (long ReceivedBytes, long SentBytes)? Parse(string content)
    {
        long received = 0;
        long sent = 0;
        var found = false;

        foreach (var line in content.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var interfaceName = line[..separator].Trim();
            if (interfaceName.Length == 0 || interfaceName == "lo")
            {
                continue;
            }

            // Receive bytes is the first field and transmit bytes the ninth.
            var fields = line[(separator + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 9
                || !long.TryParse(fields[0], out var interfaceReceived)
                || !long.TryParse(fields[8], out var interfaceSent))
            {
                continue;
            }

            received += interfaceReceived;
            sent += interfaceSent;
            found = true;
        }

        return found ? (received, sent) : null;
    }
}

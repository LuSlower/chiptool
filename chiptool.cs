using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace chiptool
{
    internal class Program
    {

        static string HandleCommand(string[] args)
        {
            chiplib chipLib = new chiplib();

            try
            {
                if (args.Length == 0) return "No command";

                string cmd = args[0].ToLowerInvariant();

                string Hex(string s) => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s.Substring(2) : s;

                switch (cmd)
                {
                    case "rdmsr":
                        if (args.Length >= 2 && uint.TryParse(Hex(args[1]), NumberStyles.HexNumber, null, out uint rdmsrAddr))
                        {
                            if (chipLib.ReadMsr(rdmsrAddr, out ulong val))
                                return $"0x{rdmsrAddr:X8} 0x{(uint)(val >> 32):X8} 0x{(uint)(val & 0xFFFFFFFF):X8}";
                            return "Failed to read MSR";
                        }
                        break;

                    case "wrmsr":
                        if (args.Length >= 4 &&
                            uint.TryParse(Hex(args[1]), NumberStyles.HexNumber, null, out uint wrAddr) &&
                            uint.TryParse(Hex(args[2]), NumberStyles.HexNumber, null, out uint wrEdx) &&
                            uint.TryParse(Hex(args[3]), NumberStyles.HexNumber, null, out uint wrEax))
                        {
                            ulong value = ((ulong)wrEdx << 32) | wrEax;
                            return chipLib.WriteMsr(wrAddr, value) ? $"0x{wrAddr:X8} 0x{wrEdx:X8} 0x{wrEax:X8}" : "Failed to write MSR";
                        }
                        break;

                    case "rdmsrb":
                        if (args.Length >= 3 && uint.TryParse(Hex(args[1]), NumberStyles.HexNumber, null, out uint rdmsrbAddr))
                        {
                            if (chipLib.ReadMsrBit(rdmsrbAddr, args[2], out ulong bitVal))
                                return $"0x{rdmsrbAddr:X8} {args[2]} 0x{bitVal:X}";
                            return "Failed to read MSR Bit";
                        }
                        break;

                    case "wrmsrb":
                        if (args.Length >= 4 &&
                            uint.TryParse(Hex(args[1]), NumberStyles.HexNumber, null, out uint wrmsrbAddr) &&
                            int.TryParse(Hex(args[3]), NumberStyles.HexNumber, null, out int bitValSet))
                        {
                            return chipLib.WriteMsrBit(wrmsrbAddr, args[2], bitValSet) ? $"0x{wrmsrbAddr:X8} {args[2]} 0x{bitValSet:X}" : "Failed to write MSR Bit";
                        }
                        break;

                    case "rdpci":
                    case "wrpci":
                    case "rdpcib":
                    case "wrpcib":
                        if (args.Length >= 4)
                        {
                            int size = int.Parse(args[1]);
                            TryParseBDF(args[2], out uint b, out uint d, out uint f);
                            int offset = int.Parse(Hex(args[3]), NumberStyles.HexNumber);

                            if (cmd.StartsWith("rd"))
                            {
                                if (cmd.EndsWith("b"))
                                {
                                    if (chipLib.ReadPciBit(b, d, f, (byte)offset, args[4], size, out ulong val))
                                        return $"{args[2]} 0x{offset:X2} {args[4]} 0x{val:X}";
                                    return "Failed to read PCI Bit";
                                }
                                else
                                {
                                    if (chipLib.ReadPci(b, d, f, (byte)offset, size, out uint val))
                                        return $"{args[2]} 0x{offset:X2} 0x{val:X}";
                                    return "Failed to read PCI";
                                }
                            }
                            else
                            {
                                uint val = uint.Parse(Hex(args[cmd.EndsWith("b") ? 5 : 4]), NumberStyles.HexNumber);
                                string bits = cmd.EndsWith("b") ? args[4] : null;
                                return cmd.EndsWith("b")
                                    ? chipLib.WritePciBit(b, d, f, (byte)offset, bits, val, size) ? $"{args[2]} 0x{offset:X2} {bits} 0x{val:X}" : "Failed to write PCI Bit"
                                    : chipLib.WritePci(b, d, f, (byte)offset, val, size) ? $"{args[2]} 0x{offset:X2} 0x{val:X}" : "Failed to write PCI";
                            }
                        }
                        break;

                    case "rdio":
                    case "wrio":
                    case "rdiob":
                    case "wriob":
                        if (args.Length >= 3)
                        {
                            int size = int.Parse(args[1]);
                            ushort port = ushort.Parse(Hex(args[2]), NumberStyles.HexNumber);
                            if (cmd.StartsWith("rd"))
                            {
                                if (cmd.EndsWith("b"))
                                {
                                    if (chipLib.ReadIoBit(port, size, args[3], out uint val))
                                        return $"0x{port:X4} {args[3]} 0x{val:X}";
                                    return "Failed to read IO Bit";
                                }
                                else
                                {
                                    if (chipLib.ReadIo(port, size, out uint val))
                                        return $"0x{port:X4} 0x{val:X8}";
                                    return "Failed to read IO";
                                }
                            }
                            else
                            {
                                uint val = cmd.EndsWith("b") ? (args[4] == "1" || args[4].ToLower() == "true" ? 1u : 0u) : uint.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                return cmd.EndsWith("b")
                                    ? chipLib.WriteIoBit(port, size, args[3], val) ? $"0x{port:X4} {args[3]} 0x{val:X}" : "Failed to write IO Bit"
                                    : chipLib.WriteIo(port, size, val) ? $"0x{port:X4} 0x{val:X}" : "Failed to write IO";
                            }
                        }
                        break;

                    case "rdmem":
                    case "wrmem":
                    case "rdmemb":
                    case "wrmemb":
                        if (args.Length >= 3)
                        {
                            int size = int.Parse(args[1]);
                            ulong addr = ulong.Parse(Hex(args[2]), NumberStyles.HexNumber);

                            if (cmd.StartsWith("rd"))
                            {
                                if (cmd.EndsWith("b"))
                                {
                                    ulong val;
                                    if (chipLib.ReadMemBit(addr, size, args[3], out val))
                                        return $"0x{addr:X8} {args[3]} 0x{val:X}";
                                    return "Failed to read MEM Bit";
                                }
                                else
                                {
                                    ulong val;
                                    if (chipLib.ReadMem(addr, size, out val))
                                        return $"0x{addr:X8} 0x{val:X}";
                                    return "Failed to read MEM";
                                }
                            }
                            else
                            {
                                ulong val = cmd.EndsWith("b") ? ulong.Parse(Hex(args[4]), NumberStyles.HexNumber) : ulong.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                return cmd.EndsWith("b")
                                    ? chipLib.WriteMemBit(addr, size, args[3], val) ? $"0x{addr:X8} {args[3]} 0x{val:X}" : "Failed to write MEM Bit"
                                    : chipLib.WriteMem(addr, size, val) ? $"0x{addr:X8} 0x{val:X}" : "Failed to write MEM";
                            }
                        }
                        break;

                    default:
                        return "Unknown command";
                }
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }

            return "Invalid arguments";
        }

        static bool TryParseBDF(string input, out uint bus, out uint dev, out uint func)
        {
            var parts = input.Split(':');
            if (parts.Length == 3 &&
                uint.TryParse(parts[0], NumberStyles.HexNumber, null, out bus) &&
                uint.TryParse(parts[1], NumberStyles.HexNumber, null, out dev) &&
                uint.TryParse(parts[2], NumberStyles.HexNumber, null, out func))
                return true;
            bus = dev = func = 0;
            return false;
        }

        static void RunAsDaemon()
        {
            var pipeName = "chiptool";
            Console.WriteLine("[DAEMON] Chiptool daemon running...");

            Task.Run(() =>
            {
                while (true)
                {
                    NamedPipeServerStream server = null;
                    try
                    {
                        server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.None);
                        server.WaitForConnection();

                        using (var reader = new StreamReader(server))
                        using (var writer = new StreamWriter(server) { AutoFlush = true })
                        {
                            string line = reader.ReadLine();
                            if (string.IsNullOrWhiteSpace(line)) return;

                            string[] split = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string output = HandleCommand(split);
                            writer.WriteLine(output);
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine("[DAEMON] Pipe error: " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DAEMON] Unexpected error: " + ex.Message);
                    }
                    finally
                    {
                        if (server != null)
                        {
                            try { server.Dispose(); } catch { }
                        }
                    }
                }
            });

            while (true)
            {
                string input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.ToLower() == "exit")
                {
                    Console.WriteLine("[DAEMON] Shutting down...");
                    break;
                }

                string[] split = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string output = HandleCommand(split);
                Console.WriteLine(output);
            }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("--h") || args.Contains("/?"))
            {
                PrintHelp();
                return;
            }

            chiplib chipLib = new chiplib();
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("No command provided");
                    return;
                }

                string cmd = args[0].ToLowerInvariant();

                string Hex(string s) => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s.Substring(2) : s;

                switch (cmd)
                {
                    case "--daemon":
                        RunAsDaemon();
                        break;

                    case "--rdmsr":
                        {
                            if (args.Length >= 2 && uint.TryParse(Hex(args[1]), NumberStyles.HexNumber, null, out uint rdmsrAddr))
                            {
                                if (chipLib.ReadMsr(rdmsrAddr, out ulong val))
                                {
                                    uint eax = (uint)(val & 0xFFFFFFFF);
                                    uint edx = (uint)(val >> 32);
                                    Console.WriteLine($"0x{rdmsrAddr:X8} 0x{edx:X8} 0x{eax:X8}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read MSR.");
                                }
                            }
                        }
                        break;

                    case "--wrmsr":
                        {
                            if (args.Length >= 4 &&
                                uint.TryParse(Hex(args[1]), NumberStyles.HexNumber, null, out uint wrAddr) &&
                                uint.TryParse(Hex(args[2]), NumberStyles.HexNumber, null, out uint wrEdx) &&
                                uint.TryParse(Hex(args[3]), NumberStyles.HexNumber, null, out uint wrEax))
                            {
                                ulong value = ((ulong)wrEdx << 32) | wrEax;
                                if (chipLib.WriteMsr(wrAddr, value))
                                {
                                    Console.WriteLine($"0x{wrAddr:X8} 0x{wrEdx:X8} 0x{wrEax:X8}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write MSR.");
                                }
                            }
                        }
                        break;

                    case "--rdpci":
                        {
                            if (args.Length >= 4)
                            {
                                int size = int.Parse(args[1]);
                                TryParseBDF(args[2], out uint b, out uint d, out uint f);
                                int offset = int.Parse(Hex(args[3]), NumberStyles.HexNumber);

                                if (chipLib.ReadPci(b, d, f, (byte)offset, size, out uint val))
                                {
                                    Console.WriteLine($"{args[2]} 0x{offset:X2} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read PCI.");
                                }
                            }
                        }
                        break;

                    case "--wrpci":
                        {
                            if (args.Length >= 5)
                            {
                                int size = int.Parse(args[1]);
                                TryParseBDF(args[2], out uint b, out uint d, out uint f);
                                int offset = int.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                uint val = uint.Parse(Hex(args[4]), NumberStyles.HexNumber);

                                if (chipLib.WritePci(b, d, f, (byte)offset, val, size))
                                {
                                    Console.WriteLine($"{args[2]} 0x{offset:X2} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write PCI.");
                                }
                            }
                        }
                        break;

                    case "--rdio":
                        {
                            if (args.Length >= 3)
                            {
                                int size = int.Parse(args[1]);
                                ushort port = ushort.Parse(Hex(args[2]), NumberStyles.HexNumber);
                                if (chipLib.ReadIo(port, size, out uint val))
                                {
                                    Console.WriteLine($"0x{port:X4} 0x{val:X8}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read IO.");
                                }
                            }
                        }
                        break;

                    case "--wrio":
                        {
                            if (args.Length >= 4)
                            {
                                int size = int.Parse(args[1]);
                                ushort port = ushort.Parse(Hex(args[2]), NumberStyles.HexNumber);
                                uint val = uint.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                if (chipLib.WriteIo(port, size, val))
                                {
                                    Console.WriteLine($"0x{port:X4} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write IO.");
                                }
                            }
                        }
                        break;

                    case "--rdmsrb":
                        {
                            if (args.Length >= 3)
                            {
                                var address = args[1];
                                var bitRange = args[2];
                                if (chipLib.ReadMsrBit(Convert.ToUInt32(address, 16), bitRange, out ulong value))
                                {
                                    Console.WriteLine($"0x{address:X8} {bitRange} 0x{value:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read MSR bit.");
                                }
                            }
                        }
                        break;

                    case "--wrmsrb":
                        {
                            if (args.Length >= 4)
                            {
                                var address = args[1];
                                var bitRange = args[2];
                                var value = uint.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                if (chipLib.WriteMsrBit(Convert.ToUInt32(address, 16), bitRange, Convert.ToInt32(value)))
                                {
                                    Console.WriteLine($"0x{address:X8} {bitRange} 0x{value}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write MSR bit.");
                                }
                            }
                        }
                        break;

                    case "--rdpcib":
                        {
                            if (args.Length >= 5)
                            {
                                int size = int.Parse(args[1]);
                                TryParseBDF(args[2], out uint b, out uint d, out uint f);
                                int offset = int.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                var bitRange = args[4];
                                if (chipLib.ReadPciBit(b, d, f, (byte)offset, bitRange, size, out ulong value))
                                {
                                    Console.WriteLine($"{args[2]} 0x{offset:X2} {bitRange} 0x{value:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read PCI bit.");
                                }
                            }
                        }
                        break;

                    case "--wrpcib":
                        {
                            if (args.Length >= 6)
                            {
                                int size = int.Parse(args[1]);
                                TryParseBDF(args[2], out uint b, out uint d, out uint f);
                                int offset = int.Parse(Hex(args[3]), NumberStyles.HexNumber);
                                var bitRange = args[4];
                                uint val = uint.Parse(Hex(args[5]), NumberStyles.HexNumber);
                                if (chipLib.WritePciBit(b, d, f, (byte)offset, bitRange, val, size))
                                {
                                    Console.WriteLine($"{args[2]} 0x{offset:X2} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write PCI bit.");
                                }
                            }
                        }
                        break;

                    case "--rdiob":
                        {
                            if (args.Length >= 4)
                            {
                                int size = int.Parse(args[1]);
                                ushort port = ushort.Parse(Hex(args[2]), NumberStyles.HexNumber);
                                var bitRange = args[3];
                                if (chipLib.ReadIoBit(port, size, bitRange, out uint val))
                                {
                                    Console.WriteLine($"0x{port:X4} {bitRange} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read IO bit.");
                                }
                            }
                        }
                        break;

                    case "--wriob":
                        {
                            if (args.Length >= 5)
                            {
                                int size = int.Parse(args[1]);
                                ushort port = ushort.Parse(Hex(args[2]), NumberStyles.HexNumber);
                                var bitRange = args[3];
                                uint val = uint.Parse(Hex(args[4]), NumberStyles.HexNumber);
                                if (chipLib.WriteIoBit(port, size, bitRange, val))
                                {
                                    Console.WriteLine($"0x{port:X4} {bitRange} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write IO bit.");
                                }
                            }
                        }
                        break;

                    case "--rdmem":
                        {
                            if (args.Length >= 3)
                            {
                                int size = int.Parse(args[1]);
                                ulong address = Convert.ToUInt64(args[2], 16);
                                if (chipLib.ReadMem(address, size, out ulong val))
                                {
                                    Console.WriteLine($"0x{address:X8} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read memory.");
                                }
                            }
                        }
                        break;

                    case "--wrmem":
                        {
                            if (args.Length >= 4)
                            {
                                int size = int.Parse(args[1]);
                                ulong address = Convert.ToUInt64(args[2], 16);
                                ulong val = Convert.ToUInt64(args[3], 16);
                                if (chipLib.WriteMem(address, size, val))
                                {
                                    Console.WriteLine($"0x{address:X8} 0x{val:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write memory.");
                                }
                            }
                        }
                        break;

                    case "--rdmemb":
                        {
                            if (args.Length >= 4)
                            {
                                int size = int.Parse(args[1]);
                                ulong address = Convert.ToUInt64(args[2], 16);
                                var bitRange = args[3];
                                if (chipLib.ReadMemBit(address, size, bitRange, out ulong value))
                                {
                                    Console.WriteLine($"0x{address:X8} {bitRange} 0x{value:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read memory bit.");
                                }
                            }
                        }
                        break;

                    case "--wrmemb":
                        {
                            if (args.Length >= 5)
                            {
                                int size = int.Parse(args[1]);
                                ulong address = Convert.ToUInt64(args[2], 16);
                                var bitRange = args[3];
                                ulong value = Convert.ToUInt64(args[4], 16);
                                if (chipLib.WriteMemBit(address, size, bitRange, value))
                                {
                                    Console.WriteLine($"0x{address:X8} {bitRange} 0x{value:X}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to write memory bit.");
                                }
                            }
                        }
                        break;

                    case "--rdpmc":
                        {
                            if (args.Length >= 2)
                            {
                                var index = args[1];
                                if (chipLib.ReadPmc(uint.Parse(index, NumberStyles.HexNumber), out ulong value))
                                {
                                    Console.WriteLine($"0x{index.PadLeft(10, '0')} 0x{value:X8}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read PMC.");
                                }
                            }
                        }
                        break;

                    case "--rdmemblk":
                        {
                            if (args.Length >= 2)
                            {
                                var index = args[1];
                                if (chipLib.ReadPmc(uint.Parse(index, NumberStyles.HexNumber), out ulong value))
                                {
                                    Console.WriteLine($"0x{index.PadLeft(10, '0')} 0x{value:X8}");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to read PMC.");
                                }
                            }
                        }
                        break;

                    case "--wrmemblk":
                        {
                            if (args.Length >= 2)
                            {
                                int paramIndex = 1; // 👈 Declaras aquí
                                paramIndex++;
                                var byteCountStr = args.ElementAtOrDefault(paramIndex++);
                                var address = args.ElementAtOrDefault(paramIndex++);
                                if (byteCountStr != null && address != null)
                                {
                                    if (!uint.TryParse(byteCountStr, out uint byteCount))
                                    {
                                        Console.WriteLine("Invalid byte count.");
                                        return;
                                    }

                                    if (args.Length - paramIndex != (int)byteCount)
                                    {
                                        Console.WriteLine("Incorrect number of bytes provided.");
                                        return;
                                    }

                                    byte[] buffer = new byte[byteCount];
                                    for (int i = 0; i < (int)byteCount; i++)
                                    {
                                        var byteStr = args[paramIndex + i];
                                        if (byteStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                        {
                                            byteStr = byteStr.Substring(2);
                                        }
                                        if (!byte.TryParse(byteStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out buffer[i]))
                                        {
                                            Console.WriteLine($"Invalid byte value: {args[paramIndex + i]}");
                                            return;
                                        }
                                    }

                                    ulong addressConverted = Convert.ToUInt64(address, 16);

                                    if (!chipLib.WriteMemBlock(addressConverted, buffer))
                                    {
                                        Console.WriteLine("Failed to write MEM block.");
                                        return;
                                    }

                                    if (chipLib.ReadMemBlock(addressConverted, byteCount, out byte[] readBuffer))
                                    {
                                        HexDump(readBuffer, byteCount, addressConverted);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to read back MEM block for verification.");
                                    }
                                }
                            }
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid command. Use --help or /? for help.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }


        private static void HexDump(byte[] data, uint size, ulong prefixAddr)
        {
            char[] ascii = new char[17];
            ascii[16] = '\0';

            for (uint i = 0; i < size; ++i)
            {
                if (i % 16 == 0)
                    Console.Write($"{prefixAddr + i:X16} | ");

                Console.Write($"{data[i]:X2} ");
                if (data[i] >= ' ' && data[i] <= '~')
                {
                    ascii[i % 16] = (char)data[i];
                }
                else
                {
                    ascii[i % 16] = '.';
                }

                if ((i + 1) % 8 == 0 || i + 1 == size)
                {
                    Console.Write(" ");
                    if ((i + 1) % 16 == 0)
                    {
                        Console.WriteLine($"|  {new string(ascii)}");
                    }
                    else if (i + 1 == size)
                    {
                        ascii[(i + 1) % 16] = '\0';
                        if ((i + 1) % 16 <= 8)
                        {
                            Console.Write(" ");
                        }
                        for (uint j = (i + 1) % 16; j < 16; ++j)
                        {
                            Console.Write("   ");
                        }
                        Console.WriteLine($"|  {new string(ascii)}");
                    }
                }
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage:");

            Console.WriteLine("\nMSR Commands");
            Console.WriteLine("  --rdmsr    <address>                                           | Read MSR");
            Console.WriteLine("  --wrmsr    <address> <edx> <eax>                               | Write MSR");
            Console.WriteLine("  --rdmsrb   <address> <bit>                                     | Read MSR Bit");
            Console.WriteLine("  --wrmsrb   <address> <bit> <value>                             | Write MSR Bit");

            Console.WriteLine("\nPCI Commands");
            Console.WriteLine("  --rdpci    <size> <bus:dev:func> <offset>                      | Read PCI configuration");
            Console.WriteLine("  --wrpci    <size> <bus:dev:func> <offset> <value>              | Write PCI configuration");
            Console.WriteLine("  --rdpcib   <size> <bus:dev:func> <offset> <bit>                | Read PCI Bit");
            Console.WriteLine("  --wrpcib   <size> <bus:dev:func> <offset> <bit> <value>        | Write PCI Bit");

            Console.WriteLine("\nI/O Commands");
            Console.WriteLine("  --rdio     <size> <port>                                       | Read I/O port");
            Console.WriteLine("  --wrio     <size> <port> <value>                               | Write I/O port");
            Console.WriteLine("  --rdiob    <size> <port> <bit>                                 | Read I/O Port Bit");
            Console.WriteLine("  --wriob    <size> <port> <bit> <value>                         | Write I/O Port Bit");

            Console.WriteLine("\nMemory Commands");
            Console.WriteLine("  --rdmem    <size> <address>                                    | Read Memory");
            Console.WriteLine("  --wrmem    <size> <address> <value>                            | Write Memory");
            Console.WriteLine("  --rdmemb   <size> <address> <bit>                              | Read Memory Bit");
            Console.WriteLine("  --wrmemb   <size> <address> <bit> <value>                      | Write Memory Bit");
            Console.WriteLine("  --rdmemblk  <bytes> <address>   | Read Memory Block (hexdump max 3584)");
            Console.WriteLine("  --wrmemblk  <bytes> <address> <byte0> <byte1> ...   | Write Memory Block (hexdump verify)");

            Console.WriteLine("\nPMC Commands");
            Console.WriteLine("  --rdpmc    <index>                                             | Read PMC Counter");

            Console.WriteLine("\nHelp");
            Console.WriteLine("  --help or /?                                                   | Show this help menu");
        }
    }
}

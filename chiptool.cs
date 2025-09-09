using System;
using System.Globalization;
using System.Linq;

namespace chiptool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("--h") || args.Contains("/?"))
            {
                PrintHelp();
                return;
            }

            chiplib chipLib = new chiplib();

            int paramIndex = 0;

            if (args[paramIndex] == "--rdmsr")
            {
                paramIndex++;
                var address = args.ElementAtOrDefault(paramIndex);
                if (address != null)
                {
                    ulong msrValue;
                    if (chipLib.ReadMsr(Convert.ToUInt32(address, 16), out msrValue))
                    {
                        uint eax = (uint)(msrValue & 0xFFFFFFFF);
                        uint edx = (uint)(msrValue >> 32);
                        Console.WriteLine($"0x{Convert.ToUInt32(address, 16):X8} 0x{edx:X8} 0x{eax:X8}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to read MSR.");
                    }
                }
            }
            else if (args[paramIndex] == "--wrmsr")
            {
                paramIndex++;
                var address = args.ElementAtOrDefault(paramIndex++);
                var edx = args.ElementAtOrDefault(paramIndex++);
                var eax = args.ElementAtOrDefault(paramIndex++);
                if (address != null && edx != null && eax != null)
                {
                    ulong value = ((ulong)Convert.ToUInt32(edx, 16) << 32) | Convert.ToUInt32(eax, 16);
                    Console.WriteLine($"0x{Convert.ToUInt32(address, 16):X8} 0x{Convert.ToUInt32(edx, 16):X8} 0x{Convert.ToUInt32(eax, 16):X8}");
                }
                else
                {
                    Console.WriteLine($"Failed to write MSR.");
                }
            }
            else if (args[paramIndex] == "--rdpci")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var busDevFunc = args.ElementAtOrDefault(paramIndex++);
                var offset = args.ElementAtOrDefault(paramIndex++);

                if (size != null && busDevFunc != null && offset != null)
                {
                    var busDevFuncParts = busDevFunc.Split(':');

                    if (busDevFuncParts.Length == 3 &&
                        uint.TryParse(busDevFuncParts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint bus) &&
                        uint.TryParse(busDevFuncParts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint dev) &&
                        uint.TryParse(busDevFuncParts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint func) &&
                        int.TryParse(size, out int sizeInt))
                    {
                        int offsetInt;

                        if (offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!int.TryParse(offset.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offsetInt))
                            {
                                Console.WriteLine("Failed to read PCI.");
                                return;
                            }
                        }
                        else
                        {
                            if (!int.TryParse(offset, out offsetInt))
                            {
                                Console.WriteLine("Failed to read PCI.");
                                return;
                            }
                        }

                        uint value;
                        if (!chipLib.ReadPci(bus, dev, func, (byte)offsetInt, sizeInt, out value))
                        {
                            Console.WriteLine("Failed to read PCI.");
                            return;
                        }

                        Console.WriteLine($"{busDevFuncParts[0]}:{busDevFuncParts[1]}:{busDevFuncParts[2]} 0x{offsetInt:X2} 0x{value:X}");
                    }
                }
            }
            else if (args[paramIndex] == "--wrpci")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var busDevFunc = args.ElementAtOrDefault(paramIndex++);
                var offset = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (size != null && busDevFunc != null && offset != null && value != null)
                {
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(2);
                    }

                    int offsetInt;

                    if (offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(offset.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offsetInt))
                        {
                            Console.WriteLine("Failed to write PCI.");
                            return;
                        }
                    }
                    else
                    {
                        if (!int.TryParse(offset, out offsetInt))
                        {
                            Console.WriteLine("Failed to write PCI.");
                            return;
                        }
                    }

                    bool success = chipLib.WritePci(
                        Convert.ToUInt32(busDevFunc.Split(':')[0], 16),
                        Convert.ToUInt32(busDevFunc.Split(':')[1], 16),
                        Convert.ToUInt32(busDevFunc.Split(':')[2], 16),
                        (byte)offsetInt,
                        uint.Parse(value, System.Globalization.NumberStyles.HexNumber),
                        int.Parse(size));

                    if (!success)
                    {
                        Console.WriteLine("Failed to write PCI.");
                        return;
                    }

                    Console.WriteLine($"{busDevFunc} 0x{offsetInt:X2} 0x{value}");
                }
            }
            else if (args[paramIndex] == "--rdio")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var port = args.ElementAtOrDefault(paramIndex++);

                if (size != null && port != null)
                {
                    if (port.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        port = port.Substring(2);
                    }

                    if (!ushort.TryParse(port, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort portParsed))
                    {
                        Console.WriteLine("Failed to read IO.");
                        return;
                    }

                    uint value;
                    if (!chipLib.ReadIo(portParsed, int.Parse(size), out value))
                    {
                        Console.WriteLine("Failed to read IO.");
                        return;
                    }

                    Console.WriteLine($"0x{port.PadLeft(4, '0')} 0x{value:X8}");
                }
            }
            else if (args[paramIndex] == "--wrio")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var port = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (size != null && port != null && value != null)
                {
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(2);
                    }

                    if (port.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        port = port.Substring(2);
                    }

                    if (!ushort.TryParse(port, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort portParsed))
                    {
                        Console.WriteLine("Failed to write IO.");
                        return;
                    }

                    bool success = chipLib.WriteIo(portParsed, int.Parse(size), uint.Parse(value, System.Globalization.NumberStyles.HexNumber));
                    if (!success)
                    {
                        Console.WriteLine("Failed to write IO.");
                        return;
                    }

                    Console.WriteLine($"0x{port.PadLeft(4, '0')} 0x{value}");
                }
            }
            else if (args[paramIndex] == "--rdmsrb")
            {
                paramIndex++;
                var address = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);

                if (address != null && bitRange != null)
                {
                    ulong value = 0;
                    bool success = chipLib.ReadMsrBit(Convert.ToUInt32(address, 16), bitRange, out value);

                    if (!success || value == ulong.MaxValue)
                    {
                        Console.WriteLine("Error: Failed to read MSR.");
                    }
                    else
                    {
                        Console.WriteLine($"0x{address.PadLeft(8, '0')} {bitRange} 0x{value:X}");
                    }
                }
            }
            else if (args[paramIndex] == "--wrmsrb")
            {
                paramIndex++;
                var address = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (address != null && bitRange != null && value != null)
                {
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(2);
                    }

                    if (!int.TryParse(value, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int newValue))
                    {
                        Console.WriteLine("Error: Failed to write MSR.");
                        return;
                    }

                    bool success = chipLib.WriteMsrBit(Convert.ToUInt32(address, 16), bitRange, newValue);

                    if (!success)
                    {
                        Console.WriteLine("Error: Failed to write MSR.");
                    }
                    else
                    {
                        Console.WriteLine($"0x{address.PadLeft(8, '0')} {bitRange} 0x{newValue:X}");
                    }
                }
            }
            else if (args[paramIndex] == "--rdpcib")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var busDevFunc = args.ElementAtOrDefault(paramIndex++);
                var offset = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);

                if (size != null && busDevFunc != null && offset != null && bitRange != null)
                {
                    int offsetInt;

                    if (offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(offset.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offsetInt))
                        {
                            Console.WriteLine("Failed to read PCI.");
                            return;
                        }
                    }
                    else
                    {
                        if (!int.TryParse(offset, out offsetInt))
                        {
                            Console.WriteLine("Failed to read PCI.");
                            return;
                        }
                    }

                    ulong value;
                    if (!chipLib.ReadPciBit(Convert.ToUInt32(busDevFunc.Split(':')[0], 16),
                                            Convert.ToUInt32(busDevFunc.Split(':')[1], 16),
                                            Convert.ToUInt32(busDevFunc.Split(':')[2], 16),
                                            (byte)offsetInt,
                                            bitRange,
                                            Convert.ToInt32(size),
                                            out value))
                    {
                        Console.WriteLine("Failed to read PCI.");
                        return;
                    }

                    Console.WriteLine($"{busDevFunc} 0x{offsetInt:X2} {bitRange} 0x{value:X}");
                }
            }
            else if (args[paramIndex] == "--wrpcib")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var busDevFunc = args.ElementAtOrDefault(paramIndex++);
                var offset = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (size != null && busDevFunc != null && offset != null && bitRange != null && value != null)
                {
                    int offsetInt;

                    if (offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(offset.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offsetInt))
                        {
                            Console.WriteLine("Failed to write PCI.");
                            return;
                        }
                    }
                    else
                    {
                        if (!int.TryParse(offset, out offsetInt))
                        {
                            Console.WriteLine("Failed to write PCI.");
                            return;
                        }
                    }

                    var bitRangeParts = bitRange.Split(':');
                    int startBit, endBit;

                    if (bitRangeParts.Length == 1)
                    {
                        if (!int.TryParse(bitRangeParts[0], out startBit))
                        {
                            Console.WriteLine("Failed to write PCI.");
                            return;
                        }
                        endBit = startBit;
                    }
                    else if (bitRangeParts.Length == 2)
                    {
                        if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
                        {
                            Console.WriteLine("Failed to write PCI.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to write PCI.");
                        return;
                    }

                    if (startBit < 0 || endBit > 31 || startBit > endBit)
                    {
                        Console.WriteLine("Failed to write PCI.");
                        return;
                    }

                    uint newValue;
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(2);
                    }
                    if (!uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out newValue))
                    {
                        Console.WriteLine("Failed to write PCI.");
                        return;
                    }

                    bool success = chipLib.WritePciBit(Convert.ToUInt32(busDevFunc.Split(':')[0], 16),
                                                       Convert.ToUInt32(busDevFunc.Split(':')[1], 16),
                                                       Convert.ToUInt32(busDevFunc.Split(':')[2], 16),
                                                       (byte)offsetInt,
                                                       bitRange,
                                                       newValue,
                                                       Convert.ToInt32(size));

                    if (!success)
                    {
                        Console.WriteLine("Failed to write PCI.");
                        return;
                    }

                    Console.WriteLine($"{busDevFunc} 0x{offsetInt:X2} {bitRange} 0x{newValue:X}");
                }
            }
            else if (args[paramIndex] == "--rdiob")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var port = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);

                if (size != null && port != null && bitRange != null)
                {
                    int startBit, endBit;

                    if (port.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        port = port.Substring(2);
                    }

                    if (!ushort.TryParse(port, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort portParsed))
                    {
                        Console.WriteLine("Failed to read IO.");
                        return;
                    }

                    var bitRangeParts = bitRange.Split(':');
                    if (bitRangeParts.Length == 1)
                    {
                        if (!int.TryParse(bitRangeParts[0], out startBit))
                        {
                            Console.WriteLine("Failed to read IO.");
                            return;
                        }
                        endBit = startBit;
                    }
                    else if (bitRangeParts.Length == 2)
                    {
                        if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
                        {
                            Console.WriteLine("Failed to read IO.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to read IO.");
                        return;
                    }

                    uint value;
                    bool success = chipLib.ReadIoBit(portParsed, int.Parse(size), bitRange, out value);
                    if (!success)
                    {
                        Console.WriteLine("Failed to read IO.");
                        return;
                    }

                    Console.WriteLine($"0x{port.PadLeft(4, '0')} {bitRange} 0x{value:X}");
                }
            }
            else if (args[paramIndex] == "--wriob")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var port = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (size != null && port != null && bitRange != null && value != null)
                {
                    int startBit, endBit;

                    if (port.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        port = port.Substring(2);
                    }

                    if (!ushort.TryParse(port, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort portParsed))
                    {
                        Console.WriteLine("Failed to write IO.");
                        return;
                    }

                    var bitRangeParts = bitRange.Split(':');
                    if (bitRangeParts.Length == 1)
                    {
                        if (!int.TryParse(bitRangeParts[0], out startBit))
                        {
                            Console.WriteLine("Failed to write IO.");
                            return;
                        }
                        endBit = startBit;
                    }
                    else if (bitRangeParts.Length == 2)
                    {
                        if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
                        {
                            Console.WriteLine("Failed to write IO.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to write IO.");
                        return;
                    }

                    uint newValue = (value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase)) ? 1U : 0U;

                    bool success = chipLib.WriteIoBit(portParsed, int.Parse(size), bitRange, newValue);
                    if (!success)
                    {
                        Console.WriteLine("Failed to write IO.");
                        return;
                    }

                    Console.WriteLine($"0x{port.PadLeft(4, '0')} {bitRange} 0x{newValue:X}");
                }
            }
            else if (args[paramIndex] == "--rdmem")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var address = args.ElementAtOrDefault(paramIndex++);
                if (size != null && address != null)
                {
                    ulong addressConverted = Convert.ToUInt64(address, 16);
                    ulong value = 0;

                    try
                    {
                        int dataSize = Convert.ToInt32(size);

                        if (dataSize == 64)
                        {
                            if (!chipLib.ReadMem(addressConverted, dataSize, out value))
                            {
                                Console.WriteLine("Failed to read MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{value:X16}");
                        }
                        else if (dataSize == 32)
                        {
                            if (!chipLib.ReadMem(addressConverted, dataSize, out value))
                            {
                                Console.WriteLine("Failed to read MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{value:X8}");
                        }
                        else if (dataSize == 16)
                        {
                            ulong shortValue;
                            if (!chipLib.ReadMem(addressConverted, dataSize, out shortValue))
                            {
                                Console.WriteLine("Failed to read MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{(ushort)shortValue:X4}");
                        }
                        else if (dataSize == 8)
                        {
                            ulong byteValue;
                            if (!chipLib.ReadMem(addressConverted, dataSize, out byteValue))
                            {
                                Console.WriteLine("Failed to read MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{(byte)byteValue:X2}");
                        }
                        else
                        {
                            Console.WriteLine("Failed to read MEM.");
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to read MEM.");
                    }
                }
            }
            else if (args[paramIndex] == "--wrmem")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var address = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (size != null && address != null && value != null)
                {
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(2);
                    }

                    ulong addressConverted = Convert.ToUInt64(address, 16);
                    ulong valueConverted = ulong.Parse(value, System.Globalization.NumberStyles.HexNumber);

                    try
                    {
                        int dataSize = Convert.ToInt32(size);

                        if (dataSize == 64)
                        {
                            bool success = chipLib.WriteMem(addressConverted, dataSize, valueConverted);
                            if (!success)
                            {
                                Console.WriteLine("Failed to write MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{valueConverted:X16}");
                        }
                        else if (dataSize == 32)
                        {
                            bool success = chipLib.WriteMem(addressConverted, dataSize, (uint)valueConverted);
                            if (!success)
                            {
                                Console.WriteLine("Failed to write MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{valueConverted:X8}");
                        }
                        else if (dataSize == 16)
                        {
                            bool success = chipLib.WriteMem(addressConverted, dataSize, (uint)valueConverted);
                            if (!success)
                            {
                                Console.WriteLine("Failed to write MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{valueConverted:X4}");
                        }
                        else if (dataSize == 8)
                        {
                            bool success = chipLib.WriteMem(addressConverted, dataSize, (uint)valueConverted);
                            if (!success)
                            {
                                Console.WriteLine("Failed to write MEM.");
                                return;
                            }
                            Console.WriteLine($"0x{addressConverted:X8} 0x{valueConverted:X2}");
                        }
                        else
                        {
                            Console.WriteLine("Failed to write MEM.");
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to write MEM.");
                    }
                }
            }
            else if (args[paramIndex] == "--rdmemb")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var address = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);

                if (size != null && address != null && bitRange != null)
                {
                    ulong addressConverted = Convert.ToUInt64(address, 16);
                    ulong value;

                    if (!chipLib.ReadMemBit(addressConverted, Convert.ToInt32(size), bitRange, out value))
                    {
                        Console.WriteLine("Failed to read MEM.");
                        return;
                    }

                    Console.WriteLine($"0x{addressConverted:X8} {bitRange} 0x{value:X}");
                }
            }
            else if (args[paramIndex] == "--wrmemb")
            {
                paramIndex++;
                var size = args.ElementAtOrDefault(paramIndex++);
                var address = args.ElementAtOrDefault(paramIndex++);
                var bitRange = args.ElementAtOrDefault(paramIndex++);
                var value = args.ElementAtOrDefault(paramIndex++);

                if (size != null && address != null && bitRange != null && value != null)
                {
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(2);
                    }

                    ulong addressConverted = Convert.ToUInt64(address, 16);
                    ulong valueConverted = ulong.Parse(value, System.Globalization.NumberStyles.HexNumber);

                    bool success = chipLib.WriteMemBit(addressConverted, Convert.ToInt32(size), bitRange, valueConverted);
                    if (!success)
                    {
                        Console.WriteLine("Failed to write MEM.");
                        return;
                    }

                    Console.WriteLine($"0x{addressConverted:X8} {bitRange} 0x{valueConverted:X}");
                }
            }
            else if (args[paramIndex] == "--rdpmc")
            {
                paramIndex++;
                var index = args.ElementAtOrDefault(paramIndex++);

                if (index != null)
                {
                    if (index.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        index = index.Substring(2);
                    }

                    bool success = chipLib.ReadPmc(uint.Parse(index, System.Globalization.NumberStyles.HexNumber), out ulong value);

                    if (success)
                    {
                        Console.WriteLine($"0x{index.PadLeft(10, '0')} 0x{value:X8}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to read PMC.");
                    }
                }
            }
            else
            {
                Console.WriteLine("Invalid command. Use --help or /? for help.");
            }
        }



        static void PrintHelp()
        {
            Console.WriteLine("Usage:");

            // MSR Commands
            Console.WriteLine("  --rdmsr <address>                   | Read MSR");
            Console.WriteLine("  --wrmsr <address> <edx> <eax>       | Write MSR");
            Console.WriteLine("  --rdmsrb <address> <bit>           | Read MSR Bit");
            Console.WriteLine("  --wrmsrb <address> <bit> <value>   | Write MSR Bit");

            // PCI Commands
            Console.WriteLine("  --rdpci <size> <bus:dev:func> <offset>   | Read PCI configuration");
            Console.WriteLine("  --wrpci <size> <bus:dev:func> <offset> <value>   | Write PCI configuration");
            Console.WriteLine("  --rdpcib <size> <bus:dev:func> <offset> <bit>   | Read PCI Bit");
            Console.WriteLine("  --wrpcib <size> <bus:dev:func> <offset> <bit> <value>   | Write PCI Bit");

            // I/O Commands
            Console.WriteLine("  --rdio <size> <port>   | Read I/O port");
            Console.WriteLine("  --wrio <size> <port> <value>   | Write I/O port");
            Console.WriteLine("  --rdiob <size> <port> <bit>   | Read I/O Port Bit");
            Console.WriteLine("  --wriob <size> <port> <bit> <value>   | Write I/O Port Bit");

            // Memory Commands
            Console.WriteLine("  --rdmem  <size> <address>   | Read Memory");
            Console.WriteLine("  --wrmem  <size> <address> <value>   | Write Memory");
            Console.WriteLine("  --rdmemb  <size> <address> <bit>   | Read Memory Bit");
            Console.WriteLine("  --wrmemb  <size><address> <bit> <value>   | Write Memory Bit");

            // PMC Commands
            Console.WriteLine("  --rdpmc <index>  | Read PMC Counter");

            // Help Command
            Console.WriteLine("  --help or /?    | Show this help menu");
        }
    }
}

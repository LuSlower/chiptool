using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using OpenLibSys;

public class chiplib
{
    private readonly Ols ols;

    public chiplib()
    {
        if (!IsAdmin())
            throw new MsrException("This application must be run as Administrator.");

        ols = new Ols();
        CheckLibraryStatus();
    }

    public static bool IsAdmin()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private void CheckLibraryStatus()
    {
        uint status = ols.GetStatus();
        if (status != (uint)Ols.Status.NO_ERROR)
            throw new MsrException($"OpenLibSys Initialization Failed. Status Code: {status}");

        uint dllStatus = ols.GetDllStatus();
        if (dllStatus != (uint)Ols.OlsDllStatus.OLS_DLL_NO_ERROR)
            throw new MsrException($"WinRing0 driver is not properly loaded. DLL Status Code: {dllStatus}");
    }

    public bool ReadPmc(uint index, out ulong value)
    {
        uint eax = 0;
        uint edx = 0;

        int result = ols.Rdpmc(index, ref eax, ref edx);

        if (result != (int)Ols.Status.NO_ERROR)
        {
            value = ((ulong)edx << 32) | eax;
            return true;
        }
        else
        {
            value = 0;
            return false;
        }
    }

    public bool ReadMsr(uint msrIndex, out ulong value)
    {
        uint eax = 0, edx = 0;

        if (ols.RdmsrTx(msrIndex, ref eax, ref edx, (UIntPtr)1) != 0)
        {
            value = ((ulong)edx << 32) | eax;
            return true;
        }
        else
        {
            value = 0;
            return false;
        }
    }

    public bool WriteMsr(uint msrIndex, ulong value, bool allProcessors = false)
    {
        uint eax = (uint)(value & 0xFFFFFFFF);
        uint edx = (uint)(value >> 32);
        int numProcessors = 1;

        if (allProcessors)
        {
            try
            {
                numProcessors = Environment.ProcessorCount;
            }
            catch
            {
                return false;
            }
        }

        ulong mask = (1UL << numProcessors) - 1;

        if (allProcessors)
        {
            bool success = true;
            for (int i = 0; i < numProcessors; i++)
            {
                ulong individualMask = 1UL << i;

                if (ols.WrmsrTx(msrIndex, eax, edx, (UIntPtr)individualMask) == 0)
                {
                    success = false;
                }
            }
            return success;
        }
        else
        {
            if (ols.WrmsrTx(msrIndex, eax, edx, (UIntPtr)mask) != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public bool ReadMsrBit(uint msrIndex, string bitRange, out ulong result)
    {
        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit))
            {
                result = ulong.MaxValue;
                return false;
            }
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
            {
                result = ulong.MaxValue;
                return false;
            }
        }
        else
        {
            result = ulong.MaxValue;
            return false;
        }

        if (startBit < 0 || endBit > 63)
        {
            result = ulong.MaxValue;
            return false;
        }

        if (startBit < endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        ulong msrValue;
        if (!ReadMsr(msrIndex, out msrValue))
        {
            result = ulong.MaxValue;
            return false;
        }

        result = 0;
        for (int bit = startBit; bit >= endBit; bit--)
        {
            ulong bitValue = (msrValue >> bit) & 1;
            result |= (bitValue << (startBit - bit));
        }

        return true;
    }

    public bool WriteMsrBit(uint msrIndex, string bitRange, int newValue, bool allProcessors = true)
    {
        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit))
            {
                return false;
            }
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (startBit < 0 || endBit > 63)
        {
            return false;
        }

        if (startBit < endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        ulong msrValue;
        if (!ReadMsr(msrIndex, out msrValue))
        {
            return false;
        }

        ulong mask = (1UL << (startBit - endBit + 1)) - 1;

        ulong adjustedValue = (ulong)newValue & mask;

        for (int bit = startBit; bit >= endBit; bit--)
        {
            if ((adjustedValue >> (startBit - bit) & 1) != 0)
            {
                msrValue |= (1UL << bit);
            }
            else
            {
                msrValue &= ~(1UL << bit);
            }
        }

        return WriteMsr(msrIndex, msrValue, allProcessors);
    }

    public bool ReadPci(uint bus, uint device, uint function, byte offset, int dataSize, out uint value)
    {
        uint pciAddress = ols.PciBusDevFunc(bus, device, function);
        value = 0;

        if (dataSize == 8)
        {
            value = ols.ReadPciConfigByte(pciAddress, offset);
        }
        else if (dataSize == 16)
        {
            value = ols.ReadPciConfigWord(pciAddress, offset);
        }
        else if (dataSize == 32)
        {
            value = ols.ReadPciConfigDword(pciAddress, offset);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool WritePci(uint bus, uint device, uint function, byte offset, ulong value, int dataSize)
    {
        uint pciAddress = ols.PciBusDevFunc(bus, device, function);

        bool result = false;
        if (dataSize == 8)
        {
            ols.WritePciConfigByte(pciAddress, offset, (byte)value);
            result = true;
        }
        else if (dataSize == 16)
        {
            ols.WritePciConfigWord(pciAddress, offset, (ushort)value);
            result = true;
        }
        else if (dataSize == 32)
        {
            ols.WritePciConfigDword(pciAddress, offset, (uint)value);
            result = true;
        }

        return result;
    }


    public bool ReadPciBit(uint bus, uint device, uint function, byte offset, string bitRange, int dataSize, out ulong result)
    {
        uint value;
        if (!ReadPci(bus, device, function, offset, dataSize, out value))
        {
            result = ulong.MaxValue;
            return false;
        }

        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            startBit = int.Parse(bitRangeParts[0]);
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            startBit = int.Parse(bitRangeParts[0]);
            endBit = int.Parse(bitRangeParts[1]);
        }
        else
        {
            result = ulong.MaxValue;
            return false;
        }

        if (startBit > endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        result = 0;
        for (int bit = startBit; bit <= endBit; bit++)
        {
            ulong bitValue = (value >> bit) & 1;
            result |= (bitValue << (bit - startBit));
        }

        return true;
    }

    public bool WritePciBit(uint bus, uint device, uint function, byte offset, string bitRange, ulong value, int dataSize)
    {
        uint pciAddress = ols.PciBusDevFunc(bus, device, function);
        uint currentValue;

        if (!ReadPci(bus, device, function, offset, dataSize, out currentValue))
        {
            return false;
        }

        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            startBit = int.Parse(bitRangeParts[0]);
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            startBit = int.Parse(bitRangeParts[0]);
            endBit = int.Parse(bitRangeParts[1]);
        }
        else
        {
            return false;
        }

        if (startBit > endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        for (int bit = startBit; bit <= endBit; bit++)
        {
            if ((value & (1UL << (bit - startBit))) != 0)
            {
                currentValue |= (1U << bit);
            }
            else
            {
                currentValue &= ~(1U << bit);
            }
        }

        WritePci(bus, device, function, offset, currentValue, dataSize);

        return true;
    }

    public bool ReadIo(ushort port, int dataSize, out uint value)
    {
        value = 0;

        if (dataSize == 8)
        {
            value = ols.ReadIoPortByte(port);
        }
        else if (dataSize == 16)
        {
            value = ols.ReadIoPortWord(port);
        }
        else if (dataSize == 32)
        {
            value = ols.ReadIoPortDword(port);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool WriteIo(ushort port, int dataSize, uint value)
    {
        bool result = false;

        if (dataSize == 8)
        {
            ols.WriteIoPortByte(port, (byte)value);
            result = true;
        }
        else if (dataSize == 16)
        {
            ols.WriteIoPortWord(port, (ushort)value);
            result = true;
        }
        else if (dataSize == 32)
        {
            ols.WriteIoPortDword(port, value);
            result = true;
        }

        return result;
    }

    public bool ReadIoBit(ushort port, int dataSize, string bitRange, out uint result)
    {
        result = uint.MaxValue;
        uint value;

        if (!ReadIo(port, dataSize, out value))
        {
            return false;
        }

        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit))
            {
                return false;
            }
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (startBit < 0 || endBit > 31)
        {
            return false;
        }

        if (startBit > endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        result = 0;
        for (int bit = startBit; bit <= endBit; bit++)
        {
            uint bitValue = (value >> bit) & 1;
            result |= (bitValue << (bit - startBit));
        }

        return true;
    }

    public bool WriteIoBit(ushort port, int dataSize, string bitRange, uint newValue)
    {
        uint currentValue;
        if (!ReadIo(port, dataSize, out currentValue))
        {
            return false;
        }

        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit))
            {
                return false;
            }
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (startBit < 0 || endBit > 31)
        {
            return false;
        }

        if (startBit > endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        for (int bit = startBit; bit <= endBit; bit++)
        {
            if (newValue == 1)
            {
                currentValue |= (1U << bit);
            }
            else
            {
                currentValue &= ~(1U << bit);
            }
        }

        WriteIo(port, dataSize, currentValue);

        return true;
    }

    [DllImport("inpoutx64.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr MapPhysToLin(IntPtr physicalAddress, uint size, ref IntPtr handle);

    [DllImport("inpoutx64.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool UnmapPhysicalMemory(IntPtr handle, IntPtr virtualAddress);
    public bool ReadMem(ulong physicalAddress, int dataSize, out ulong value)
    {
        value = 0;
        IntPtr handle = IntPtr.Zero;
        IntPtr ptr = MapPhysicalAddress(physicalAddress, dataSize, ref handle);
        if (ptr == IntPtr.Zero) return false;

        byte[] buffer = new byte[dataSize / 8];

        Marshal.Copy(ptr, buffer, 0, buffer.Length);

        UnmapPhysicalMemory(handle, ptr);

        if (dataSize == 8)
        {
            value = buffer[0];
        }
        else if (dataSize == 16)
        {
            value = BitConverter.ToUInt16(buffer, 0);
        }
        else if (dataSize == 32)
        {
            value = BitConverter.ToUInt32(buffer, 0);
        }
        else if (dataSize == 64)
        {
            value = BitConverter.ToUInt64(buffer, 0);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool WriteMem(ulong physicalAddress, int dataSize, ulong value)
    {
        IntPtr handle = IntPtr.Zero;
        IntPtr ptr = MapPhysicalAddress(physicalAddress, dataSize, ref handle);

        if (ptr == IntPtr.Zero)
        {
            return false;
        }

        byte[] buffer = new byte[dataSize / 8];

        if (dataSize == 8)
        {
            buffer[0] = (byte)value;
        }
        else if (dataSize == 16)
        {
            buffer = BitConverter.GetBytes((ushort)value);
        }
        else if (dataSize == 32)
        {
            buffer = BitConverter.GetBytes((uint)value);
        }
        else if (dataSize == 64)
        {
            buffer = BitConverter.GetBytes(value);
        }

        try
        {
            Marshal.Copy(buffer, 0, ptr, buffer.Length);
            UnmapPhysicalMemory(handle, ptr);
            return true;
        }
        catch (Exception)
        {
            UnmapPhysicalMemory(handle, ptr);
            return false;
        }
    }

    public bool ReadMemBit(ulong physicalAddress, int dataSize, string bitRange, out ulong result)
    {
        result = ulong.MaxValue;
        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit))
            {
                return false;
            }
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (startBit < 0 || endBit > 63)
        {
            return false;
        }

        if (startBit > endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        ulong value;
        if (!ReadMem(physicalAddress, dataSize, out value))
        {
            return false;
        }

        result = 0;
        for (int bit = startBit; bit <= endBit; bit++)
        {
            ulong bitValue = (value >> bit) & 1;
            result |= (bitValue << (bit - startBit));
        }

        return true;
    }

    public bool WriteMemBit(ulong physicalAddress, int dataSize, string bitRange, ulong newValue)
    {
        ulong currentValue;
        if (!ReadMem(physicalAddress, dataSize, out currentValue))
        {
            return false;
        }

        var bitRangeParts = bitRange.Split(':');
        int startBit, endBit;

        if (bitRangeParts.Length == 1)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit))
            {
                return false;
            }
            endBit = startBit;
        }
        else if (bitRangeParts.Length == 2)
        {
            if (!int.TryParse(bitRangeParts[0], out startBit) || !int.TryParse(bitRangeParts[1], out endBit))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (startBit < 0 || endBit > 63)
        {
            return false;
        }

        if (startBit > endBit)
        {
            int temp = startBit;
            startBit = endBit;
            endBit = temp;
        }

        for (int bit = startBit; bit <= endBit; bit++)
        {
            if ((newValue & (1UL << (bit - startBit))) != 0)
            {
                currentValue |= (1UL << bit);
            }
            else
            {
                currentValue &= ~(1UL << bit);
            }
        }

        return WriteMem(physicalAddress, dataSize, currentValue);
    }

    private IntPtr MapPhysicalAddress(ulong physicalAddress, int dataSize, ref IntPtr handle)
    {
        IntPtr address = (IntPtr)physicalAddress;
        IntPtr mappedAddress = MapPhysToLin(address, (uint)(dataSize / 8), ref handle);
        return mappedAddress;
    }

}
public class MsrException : Exception
{
    public MsrException(string message) : base(message) { }
}

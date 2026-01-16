/*  WinUSBNet library
 *  (C) 2010 Thomas Bleeker (www.madwizard.org)
 *  
 *  Licensed under the MIT license, see license.txt or:
 *  http://www.opensource.org/licenses/mit-license.php
 */

// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace QCEDL.NET.USB
{
    public class USBExtensions
    {
        public static (string?, string?)[] GetDeviceInfos(Guid guid)
        {
            List<(string?, string?)> deviceInfos = [];

            nint deviceInfoSet = nint.Zero;
            try
            {
                deviceInfoSet = SetupDiGetClassDevs(ref guid, nint.Zero, nint.Zero,
                    DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (deviceInfoSet == INVALID_HANDLE_VALUE)
                {
                    throw new Win32Exception("Failed to enumerate devices.");
                }

                int memberIndex = 0;
                while (true)
                {
                    // Begin with 0 and increment through the device information set until
                    // no more devices are available.					
                    SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new();

                    // The cbSize element of the deviceInterfaceData structure must be set to
                    // the structure's size in bytes. 
                    // The size is 28 bytes for 32-bit code and 32 bytes for 64-bit code.
                    deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

                    bool success = SetupDiEnumDeviceInterfaces(deviceInfoSet, nint.Zero, ref guid, memberIndex, ref deviceInterfaceData);

                    // Find out if a device information set was retrieved.
                    if (!success)
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        if (lastError == ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        throw new Win32Exception("Failed to get device interface.");
                    }
                    // A device is present.

                    int bufferSize = 0;

                    success = SetupDiGetDeviceInterfaceDetail
                        (deviceInfoSet,
                        ref deviceInterfaceData,
                        nint.Zero,
                        0,
                        ref bufferSize,
                        nint.Zero);

                    if (!success && Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
                    {
                        throw new Win32Exception("Failed to get interface details buffer size.");
                    }

                    nint detailDataBuffer = nint.Zero;
                    try
                    {
                        // Allocate memory for the SP_DEVICE_INTERFACE_DETAIL_DATA structure using the returned buffer size.
                        detailDataBuffer = Marshal.AllocHGlobal(bufferSize);

                        // Store cbSize in the first bytes of the array. The number of bytes varies with 32- and 64-bit systems.

                        Marshal.WriteInt32(detailDataBuffer, nint.Size == 4 ? 4 + Marshal.SystemDefaultCharSize : 8);

                        // Call SetupDiGetDeviceInterfaceDetail again.
                        // This time, pass a pointer to DetailDataBuffer
                        // and the returned required buffer size.

                        // build a DevInfo Data structure
                        SP_DEVINFO_DATA da = new();
                        da.cbSize = Marshal.SizeOf(da);

                        success = SetupDiGetDeviceInterfaceDetail
                            (deviceInfoSet,
                            ref deviceInterfaceData,
                            detailDataBuffer,
                            bufferSize,
                            ref bufferSize,
                            ref da);

                        if (!success)
                        {
                            throw new Win32Exception("Failed to get device interface details.");
                        }

                        // Skip over cbsize (4 bytes) to get the address of the devicePathName.

                        nint pDevicePathName = new(detailDataBuffer.ToInt64() + 4);
                        string? pathName = Marshal.PtrToStringUni(pDevicePathName);

                        // Get the String containing the devicePathName.

                        string BusName = GetBusName(deviceInfoSet, da);

                        deviceInfos.Add((pathName, BusName));
                    }
                    finally
                    {
                        if (detailDataBuffer != nint.Zero)
                        {
                            Marshal.FreeHGlobal(detailDataBuffer);
                            detailDataBuffer = nint.Zero;
                        }
                    }
                    memberIndex++;
                }
            }
            finally
            {
                if (deviceInfoSet != nint.Zero && deviceInfoSet != INVALID_HANDLE_VALUE)
                {
                    SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }

            return [.. deviceInfos];
        }

        private static string GetBusName(nint deviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            string BusName = "";

            try
            {
                BusName = GetStringProperty(deviceInfoSet, deviceInfoData, new DEVPROPKEY(new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), 4));
            }
            catch { }

            return BusName;
        }

        // Heathcliff74
        // todo: is the queried data always available, or should we check ERROR_INVALID_DATA?
        private static string GetStringProperty(nint deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY property)
        {
            byte[] buffer = GetProperty(deviceInfoSet, deviceInfoData, property, out uint propertyType);
            if (propertyType != 0x00000012) // DEVPROP_TYPE_STRING
            {
                throw new Exception("Invalid registry type returned for device property.");
            }

            // sizof(char), 2 bytes, are removed to leave out the string terminator
            return Encoding.Unicode.GetString(buffer, 0, buffer.Length - sizeof(char));
        }

        // Heathcliff74
        private static byte[] GetProperty(nint deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY property, out uint propertyType)
        {
            if (!SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref property, out propertyType, null, 0, out int requiredSize, 0) && Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
            {
                throw new Win32Exception("Failed to get buffer size for device registry property.");
            }

            byte[] buffer = new byte[requiredSize];

            if (!SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref property, out propertyType, buffer, buffer.Length, out requiredSize, 0))
            {
                throw new Win32Exception("Failed to get device registry property.");
            }

            return buffer;
        }

        private const int DIGCF_PRESENT = 2;
        private const int DIGCF_DEVICEINTERFACE = 0X10;
        public static readonly nint INVALID_HANDLE_VALUE = new(-1);
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        private struct SP_DEVICE_INTERFACE_DATA
        {
            internal int cbSize;
            internal Guid InterfaceClassGuid;
            internal int Flags;
            internal nint Reserved;
        }

        private struct SP_DEVINFO_DATA
        {
            internal int cbSize;
            internal Guid ClassGuid;
            internal int DevInst;
            internal nint Reserved;
        }

        // Device Property
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct DEVPROPKEY(Guid ifmtid, uint ipid)
        {
            public Guid fmtid = ifmtid;
            public uint pid = ipid;
        }

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(nint DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, nint DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, ref int RequiredSize, nint DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(nint DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, nint DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, ref int RequiredSize, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(nint DeviceInfoSet, nint DeviceInfoData, ref Guid InterfaceClassGuid, int MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern int SetupDiDestroyDeviceInfoList(nint DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern nint SetupDiGetClassDevs(ref Guid ClassGuid, nint Enumerator, nint hwndParent, int Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern unsafe bool SetupDiGetDeviceProperty(nint deviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref DEVPROPKEY propertyKey, out uint propertyType, byte[]? propertyBuffer, int propertyBufferSize, out int requiredSize, uint flags);
    }
}

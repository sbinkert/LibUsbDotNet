// Copyright � 2006-2010 Travis Robinson. All rights reserved.
// 
// website: http://sourceforge.net/projects/libusbdotnet
// e-mail:  libusbdotnet@gmail.com
// 
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation; either version 2 of the License, or 
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
// for more details.
// 
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA. or 
// visit www.gnu.org.
// 
// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using LibUsbDotNet.Descriptors;
using LibUsbDotNet.Info;
using LibUsbDotNet.Internal;
using LibUsbDotNet.Main;
using LibUsb.Common;
using LibUsbDotNet.LudnMonoLibUsb;
using MonoLibUsb;

namespace LibUsbDotNet
{
    /// <summary>Contains non-driver specific USB device communication members.</summary>
    /// <remarks>
    /// This class is compatible with WinUSB, LibUsb-Win32, and linux libusb v1.x. 
    /// Platform independent applications should only use usb device members from this class.
    /// If more functionality is required, it is up to the application to handle multi-driver
    /// and/or multi-platfrom requirements.
    /// </remarks>
    public abstract partial class UsbDevice : IDisposable
    {
        #region Enumerations

        /// <summary>
        /// Driver modes enumeration. See the UsbDevice.<see cref="UsbDevice.DriverMode"/> property.
        /// </summary>
        public enum DriverModeType
        {
            /// <summary>
            /// Not yet determined.
            /// </summary>
            Unknown,
            /// <summary>
            /// Using LibUsb kernel driver (Legacy or Native) on windows.
            /// </summary>
            LibUsb,
            /// <summary>
            /// Using WinUsb user-mode driver on windows.
            /// </summary>
            WinUsb,
            /// <summary>
            /// Using Libusb 1.0 driver on linux.
            /// </summary>
            MonoLibUsb,
            /// <summary>
            /// Using Libusb 1.0 windows backend driver on windows.
            /// </summary>
            LibUsbWinBack
        }

        #endregion

        protected readonly UsbEndpointList mActiveEndpoints;
        internal readonly UsbApiBase mUsbApi;
        protected DeviceDescriptor mCachedDeviceDescriptor;
        protected List<UsbConfigInfo> mConfigs;
        protected int mCurrentConfigValue = -1;
        protected UsbDeviceInfo mDeviceInfo;
        protected SafeHandle mUsbHandle;

        protected readonly Byte[] UsbAltInterfaceSettings = new byte[UsbConstants.MAX_DEVICES];
        protected readonly List<int> mClaimedInterfaces = new List<int>();

        protected UsbDevice(UsbApiBase usbApi, SafeHandle usbHandle)
        {
            mUsbApi = usbApi;
            mUsbHandle = usbHandle;
            mActiveEndpoints = new UsbEndpointList();
        }

        internal DeviceDescriptor CachedDeviceDescriptor
        {
            get { return mCachedDeviceDescriptor; }
        }

        ///<summary>
        /// Gets all available configurations for this <see cref="UsbDevice"/>
        ///</summary>
        /// <remarks>
        /// The first time this property is accessed it will query the <see cref="UsbDevice"/> for all configurations.
        /// Subsequent request will return a cached copy of all configurations.
        /// </remarks>
        public virtual ReadOnlyCollection<UsbConfigInfo> Configs
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the actual device descriptor the the current <see cref="UsbDevice"/>.
        /// </summary>
        public virtual UsbDeviceInfo Info
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets a value indication if the device handle is valid.
        /// </summary>
        public bool IsOpen
        {
            get { return ((mUsbHandle != null) && !mUsbHandle.IsClosed) && !mUsbHandle.IsInvalid; }
        }

        /// <summary>
        /// A list of endpoints that have beened opened by this <see cref="UsbDevice"/> class.
        /// </summary>
        public UsbEndpointList ActiveEndpoints
        {
            get { return mActiveEndpoints; }
        }


        public SafeHandle Handle
        {
            get { return mUsbHandle; }
        }

        /// <summary>
        /// Returns the DriverMode this USB device is using.
        /// </summary>
        public abstract DriverModeType DriverMode { get; }


        /// <summary>
        /// Closes the <see cref="UsbDevice"/> and disposes any <see cref="ActiveEndpoints"/>.
        /// </summary>
        /// <returns>True on success.</returns>
        public abstract bool Close();

        ///<summary>
        /// Opens the USB device handle.
        ///</summary>
        ///<returns>
        ///True if the device is already opened or was opened successfully.
        ///False if the device does not exists or is no longer valid.  
        ///</returns>
        public abstract bool Open();

        /// <summary>
        /// Transmits control data over a default control endpoint.
        /// </summary>
        /// <param name="setupPacket">An 8-byte setup packet which contains parameters for the control request. 
        /// See section 9.3 USB Device Requests of the Universal Serial Bus Specification Revision 2.0 for more information. </param>
        /// <param name="buffer">Data to be sent/received from the device.</param>
        /// <param name="bufferLength">Length of the buffer param.</param>
        /// <param name="lengthTransferred">Number of bytes sent or received (depends on the direction of the control transfer).</param>
        /// <returns>True on success.</returns>
        public virtual bool ControlTransfer(ref UsbSetupPacket setupPacket, IntPtr buffer, int bufferLength, out int lengthTransferred)
        {
            bool bSuccess = mUsbApi.ControlTransfer(mUsbHandle, setupPacket, buffer, bufferLength, out lengthTransferred);

            if (!bSuccess)
                UsbError.Error(ErrorCode.Win32Error, Marshal.GetLastWin32Error(), "ControlTransfer", this);

            return bSuccess;
        }

        /// <summary>
        /// Transmits control data over a default control endpoint.
        /// </summary>
        /// <param name="setupPacket">An 8-byte setup packet which contains parameters for the control request. 
        /// See section 9.3 USB Device Requests of the Universal Serial Bus Specification Revision 2.0 for more information. </param>
        /// <param name="buffer">Data to be sent/received from the device.</param>
        /// <param name="bufferLength">Length of the buffer param.</param>
        /// <param name="lengthTransferred">Number of bytes sent or received (depends on the direction of the control transfer).</param>
        /// <returns>True on success.</returns>
        public virtual bool ControlTransfer(ref UsbSetupPacket setupPacket, object buffer, int bufferLength, out int lengthTransferred)
        {
            PinnedHandle pinned = new PinnedHandle(buffer);
            bool bSuccess = ControlTransfer(ref setupPacket, pinned.Handle, bufferLength, out lengthTransferred);
            pinned.Dispose();

            return bSuccess;
        }

        /// <summary>
        /// Gets the USB devices active configuration value. 
        /// </summary>
        /// <param name="config">The active configuration value. A zero value means the device is not configured and a non-zero value indicates the device is configured.</param>
        /// <returns>True on success.</returns>
        public virtual bool GetConfiguration(out byte config)
        {
            config = 0;
            byte[] buf = new byte[1];
            int uTransferLength;

            UsbSetupPacket setupPkt = new UsbSetupPacket();
            setupPkt.RequestType = (byte) EndpointDirection.In | (byte) UsbRequestType.TypeStandard |
                                   (byte) UsbRequestRecipient.RecipDevice;
            setupPkt.Request = (byte) StandardRequest.GetConfiguration;
            setupPkt.Value = 0;
            setupPkt.Index = 0;
            setupPkt.Length = 1;

            bool bSuccess = ControlTransfer(ref setupPkt, buf, buf.Length, out uTransferLength);
            if (bSuccess && uTransferLength == 1)
            {
                config = buf[0];
                mCurrentConfigValue = config;
                return true;
            }
            UsbError.Error(ErrorCode.Win32Error, Marshal.GetLastWin32Error(), "GetConfiguration", this);
            return false;
        }

        /// <summary>
        /// Gets a descriptor from the device. See <see cref="DescriptorType"/> for more information.
        /// </summary>
        /// <param name="descriptorType">The descriptor type ID to retrieve; this is usually one of the <see cref="DescriptorType"/> enumerations.</param>
        /// <param name="index">Descriptor index.</param>
        /// <param name="langId">Descriptor language id.</param>
        /// <param name="buffer">Memory to store the returned descriptor in.</param>
        /// <param name="bufferLength">Length of the buffer parameter in bytes.</param>
        /// <param name="transferLength">The number of bytes transferred to buffer upon success.</param>
        /// <returns>True on success.</returns>
        public virtual bool GetDescriptor(byte descriptorType, byte index, short langId, IntPtr buffer, int bufferLength, out int transferLength)
        {
            transferLength = 0;

            bool wasOpen = IsOpen;
            if (!wasOpen) Open();
            if (!IsOpen) return false;

            bool bSuccess = mUsbApi.GetDescriptor(mUsbHandle, descriptorType, index, (ushort) langId, buffer, bufferLength, out transferLength);

            if (!bSuccess)
                UsbError.Error(ErrorCode.Win32Error, Marshal.GetLastWin32Error(), "GetDescriptor", this);

            if (!wasOpen && IsOpen) Close();

            return bSuccess;
        }


        /// <summary>
        /// Opens a <see cref="EndpointType.Bulk"/> endpoint for writing
        /// </summary>
        /// <param name="writeEndpointID">Endpoint number for read operations.</param>
        /// <returns>A <see cref="UsbEndpointWriter"/> class ready for writing. If the specified endpoint is already been opened, the original <see cref="UsbEndpointWriter"/> class is returned.</returns>
        public virtual UsbEndpointWriter OpenEndpointWriter(WriteEndpointID writeEndpointID) { return OpenEndpointWriter(writeEndpointID, EndpointType.Bulk); }

        /// <summary>
        /// Opens an endpoint for writing
        /// </summary>
        /// <param name="writeEndpointID">Endpoint number for read operations.</param>
        /// <param name="endpointType">The type of endpoint to open.</param>
        /// <returns>A <see cref="UsbEndpointWriter"/> class ready for writing. If the specified endpoint is already been opened, the original <see cref="UsbEndpointWriter"/> class is returned.</returns>
        public virtual UsbEndpointWriter OpenEndpointWriter(WriteEndpointID writeEndpointID, EndpointType endpointType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a descriptor from the device. See <see cref="DescriptorType"/> for more information.
        /// </summary>
        /// <param name="descriptorType">The descriptor type ID to retrieve; this is usually one of the <see cref="DescriptorType"/> enumerations.</param>
        /// <param name="index">Descriptor index.</param>
        /// <param name="langId">Descriptor language id.</param>
        /// <param name="buffer">Memory to store the returned descriptor in.</param>
        /// <param name="bufferLength">Length of the buffer parameter in bytes.</param>
        /// <param name="transferLength">The number of bytes transferred to buffer upon success.</param>
        /// <returns>True on success.</returns>
        public bool GetDescriptor(byte descriptorType, byte index, short langId, object buffer, int bufferLength, out int transferLength)
        {
            PinnedHandle pinned = new PinnedHandle(buffer);
            bool bSuccess = GetDescriptor(descriptorType, index, langId, pinned.Handle, bufferLength, out transferLength);
            pinned.Dispose();

            return bSuccess;
        }


        /// <summary>
        /// Asking for the zero'th index is special - it returns a string
        /// descriptor that contains all the language IDs supported by the
        /// device. Typically there aren't many - often only one. The
        /// language IDs are 16 bit numbers, and they start at the third byte
        /// in the descriptor. See USB 2.0 specification, section 9.6.7, for
        /// more information on this. 
        /// </summary>
        /// <returns>A collection of LCIDs that the current <see cref="UsbDevice"/> supports.</returns>
        public bool GetLangIDs(out short[] langIDs)
        {
            LangStringDescriptor sd = new LangStringDescriptor(UsbDescriptor.Size + (16*sizeof (short)));

            int ret;
            bool bSuccess = GetDescriptor((byte) DescriptorType.String, 0, 0, sd.Ptr, sd.MaxSize, out ret);
            if (bSuccess && ret == sd.Length)
            {
                bSuccess = sd.Get(out langIDs);
            }
            else
            {
                langIDs = new short[0];
                UsbError.Error(ErrorCode.Win32Error, Marshal.GetLastWin32Error(), "GetLangIDs", this);
            }
            sd.Free();
            return bSuccess;
        }

        /// <summary>
        /// Gets a <see cref="DescriptorType.String"/> descriptor from the device.
        /// </summary>
        /// <param name="stringData">Buffer to store the returned string in upon success.</param>
        /// <param name="langId">The language ID to retrieve the string in. (0x409 for english).</param>
        /// <param name="stringIndex">The string index to retrieve.</param>
        /// <returns>True on success.</returns>
        public bool GetString(out string stringData, short langId, byte stringIndex)
        {
            stringData = null;
            int iTransferLength;
            LangStringDescriptor sd = new LangStringDescriptor(255);
            bool bSuccess = GetDescriptor((byte) DescriptorType.String, stringIndex, langId, sd.Ptr, sd.MaxSize, out iTransferLength);

            if (bSuccess && iTransferLength > UsbDescriptor.Size && sd.Length == iTransferLength)
                bSuccess = sd.Get(out stringData);
            else if (!bSuccess)
                UsbError.Error(ErrorCode.Win32Error, Marshal.GetLastWin32Error(), "GetString:GetDescriptor", this);
            else
                stringData = String.Empty;

            return bSuccess;
        }


        /// <summary>
        /// Opens a <see cref="EndpointType.Bulk"/> endpoint for reading
        /// </summary>
        /// <param name="readEndpointID">Endpoint number for read operations.</param>
        /// <returns>A <see cref="UsbEndpointReader"/> class ready for reading. If the specified endpoint is already been opened, the original <see cref="UsbEndpointReader"/> class is returned.</returns>
        public UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID) { return OpenEndpointReader(readEndpointID, UsbEndpointReader.DefReadBufferSize); }

        /// <summary>
        /// Opens a <see cref="EndpointType.Bulk"/> endpoint for reading
        /// </summary>
        /// <param name="readEndpointID">Endpoint number for read operations.</param>
        /// <param name="readBufferSize">Size of the read buffer allocated for the <see cref="UsbEndpointReader.DataReceived"/> event.</param>
        /// <returns>A <see cref="UsbEndpointReader"/> class ready for reading. If the specified endpoint is already been opened, the original <see cref="UsbEndpointReader"/> class is returned.</returns>
        public UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID, int readBufferSize) { return OpenEndpointReader(readEndpointID, readBufferSize, EndpointType.Bulk); }

        /// <summary>
        /// Opens an endpoint for reading
        /// </summary>
        /// <param name="readEndpointID">Endpoint number for read operations.</param>
        /// <param name="readBufferSize">Size of the read buffer allocated for the <see cref="UsbEndpointReader.DataReceived"/> event.</param>
        /// <param name="endpointType">The type of endpoint to open.</param>
        /// <returns>A <see cref="UsbEndpointReader"/> class ready for reading. If the specified endpoint is already been opened, the original <see cref="UsbEndpointReader"/> class is returned.</returns>
        public virtual UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID, int readBufferSize, EndpointType endpointType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the selected alternate interface of the specified interface.
        /// </summary>
        /// <param name="interfaceID">The interface settings number (index) to retrieve the selected alternate interface setting for.</param>
        /// <param name="selectedAltInterfaceID">The alternate interface setting selected for use with the specified interface.</param>
        /// <returns>True on success.</returns>
        public bool GetAltInterfaceSetting(byte interfaceID, out byte selectedAltInterfaceID)
        {
            byte[] buf = new byte[1];
            int uTransferLength;

            UsbSetupPacket setupPkt = new UsbSetupPacket();
            setupPkt.RequestType = (byte) EndpointDirection.In | (byte) UsbRequestType.TypeStandard |
                                   (byte) UsbRequestRecipient.RecipInterface;
            setupPkt.Request = (byte) StandardRequest.GetInterface;
            setupPkt.Value = 0;
            setupPkt.Index = interfaceID;
            setupPkt.Length = 1;

            bool bSuccess = ControlTransfer(ref setupPkt, buf, buf.Length, out uTransferLength);
            if (bSuccess && uTransferLength == 1)
                selectedAltInterfaceID = buf[0];
            else
                selectedAltInterfaceID = 0;

            return bSuccess;
        }

        /// <summary>
        /// Gets the device path.
        /// </summary>
        /// <remarks>
        /// If two devices are physically connected to the same system at the same time,
        /// they will have different device paths.
        /// 
        /// This makes it possible to distinguish two devices of the same kind (same vid, pid, rev, serial#),
        /// (especially when the manufacturer is lazy and does not bother to assign serial numbers).
        /// </remarks>
        public abstract string DevicePath { get; }

        void IDisposable.Dispose()
        {
            Close();
        }
    }
}
using System.Collections.Generic;
using System.ComponentModel;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace CNC_App
{
    /// <summary>
    /// ����� ������ � ������������
    /// </summary>
    public static class Controller
    {
        #region ������������� ������� �����������

        public delegate void DeviceEventConnect(object sender);                              // ����������� �� ��������� �����
        public delegate void DeviceEventDisconnect(object sender, DeviceEventArgsMessage e); // ����������� �� ������/����������� �����
        public delegate void DeviceEventNewData(object sender);                              // ����������� ��� �������� ����� ������ ������������
        public delegate void DeviceEventNewMessage(object sender, DeviceEventArgsMessage e); // ��� ������� ����������� ��������� ��������� � ���������

        /// <summary>
        /// ������� ��� �������� ����������� � �����������
        /// </summary>
        public static event DeviceEventConnect WasConnected;
        /// <summary>
        /// ������� ��� ���������� �� �����������, ��� ������� ����� � ������������
        /// </summary>
        public static event DeviceEventDisconnect WasDisconnected;
        /// <summary>
        /// �������� ����� ������ �� �����������
        /// </summary>
        public static event DeviceEventNewData NewDataFromController;
        /// <summary>
        /// ������� ������ �������� (��� ������� �����)
        /// </summary>
        public static event DeviceEventNewMessage Message;

        #endregion
        
        #region ���������� ���������

        /// <summary>
        /// ������� ����� � ������������
        /// </summary>
        private static bool _connected;

        /// <summary>
        /// ����� ��� ���������, ������� ������ � ����������
        /// </summary>
        private static BackgroundWorker _theads;

        private static UsbDevice _myUsbDevice;
        private static ErrorCode _ec;
        private static UsbEndpointReader _usbReader;
        private static UsbEndpointWriter _usbWriter;

        #endregion

        #region �������� ��� ������� ����� � ����������

        /// <summary>
        /// ���������� ���������� � ������� �����
        /// </summary>
        public static bool Connected
        {
            get
            {
                return _connected;
            }
        }

        /// <summary>
        /// �������� �������� ��������
        /// </summary>
        public static int ShpindelMoveSpeed
        {
            get
            {
                return deviceInfo.shpindel_MoveSpeed;
            }
        }

        /// <summary>
        /// ����� ����������� ����������
        /// </summary>
        public static int NumberComleatedInstructions
        {
            get
            {
                return deviceInfo.NuberCompleatedInstruction;
            }
        }

        /// <summary>
        /// �������� ������� �� ��������
        /// </summary>
        public static bool SpindelOn
        {
            get { return deviceInfo.shpindel_Enable; }
        }

        /// <summary>
        /// �������� ������������� �� ��������� ���������
        /// </summary>
        public static bool EstopOn
        {
            get { return deviceInfo.Estop; }
        }

        /// <summary>
        /// �������� ������� �����, � ����������� �����������
        /// </summary>
        /// <returns>������, �������� �� �������� ����������� ������</returns>
        public static bool TestAllowActions()
        {
            if (!Connected)
            {
                //StringError = @"����������� ����� � ������������!";
                return false;
            }

            return true;
        }


        /// <summary>
        /// ������ ���������� ������
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static int AvailableBufferSize
        {
            get
            {
                return deviceInfo.FreebuffSize;
            }
        }

        #endregion

        #region ����� ���������� �������

        /// <summary>
        /// ������ ���������� ������ � �����������
        /// </summary>
        /// <param name="readBuffer"></param>
        private static void ParseInfo(IList<byte> readBuffer)
        {

            int ttm = (int) (((readBuffer[22]*65536) + (readBuffer[21]*256) + (readBuffer[20]))/2.1);

            if (ttm > 5000) return;

            //TODO: ������ � ��2 ������ ����, ������� ��������� �� ����, ��������
            //if (readBuffer[10] == 0x58 && readBuffer[11] == 0x02 && readBuffer[22] == 0x20 && readBuffer[23] == 0x02) return;

            deviceInfo.FreebuffSize = readBuffer[1];


            deviceInfo.shpindel_MoveSpeed = 0; 

            if (Setting.DeviceModel == DeviceModel.MK1)
            {
                deviceInfo.shpindel_MoveSpeed = (int)(((readBuffer[22] * 65536) + (readBuffer[21] * 256) + (readBuffer[20])) / 2.1);
            }

            if (Setting.DeviceModel == DeviceModel.MK2)
            {
                deviceInfo.shpindel_MoveSpeed = (int)(((readBuffer[22] * 65536) + (readBuffer[21] * 256) + (readBuffer[20])) / 1.341);
            }


             



            deviceInfo.AxesX_PositionPulse = (readBuffer[27] * 16777216) + (readBuffer[26] * 65536) + (readBuffer[25] * 256) + (readBuffer[24]);
            deviceInfo.AxesY_PositionPulse = (readBuffer[31] * 16777216) + (readBuffer[30] * 65536) + (readBuffer[29] * 256) + (readBuffer[28]);
            deviceInfo.AxesZ_PositionPulse = (readBuffer[35] * 16777216) + (readBuffer[34] * 65536) + (readBuffer[33] * 256) + (readBuffer[32]);

            deviceInfo.AxesX_LimitMax = (readBuffer[15] & (1 << 0)) != 0;
            deviceInfo.AxesX_LimitMin = (readBuffer[15] & (1 << 1)) != 0;
            deviceInfo.AxesY_LimitMax = (readBuffer[15] & (1 << 2)) != 0;
            deviceInfo.AxesY_LimitMin = (readBuffer[15] & (1 << 3)) != 0;
            deviceInfo.AxesZ_LimitMax = (readBuffer[15] & (1 << 4)) != 0;
            deviceInfo.AxesZ_LimitMin = (readBuffer[15] & (1 << 5)) != 0;

            deviceInfo.NuberCompleatedInstruction = readBuffer[9] * 16777216 + (readBuffer[8] * 65536) + (readBuffer[7] * 256) + (readBuffer[6]);

            SuperByte bb = new SuperByte(readBuffer[19]);

            deviceInfo.shpindel_Enable = bb.Bit0;

            SuperByte bb2 = new SuperByte(readBuffer[14]);
            deviceInfo.Estop = bb2.Bit7;
        }

        private static void AddMessage(string ss)
        {
            if (Message != null) Message(null, new DeviceEventArgsMessage(ss));
        }

        private static bool CompareArray(byte[] arr1, byte[] arr2)
        {
            if (arr1 == null || arr2 == null) return false;

            //��������� 64 �����
            bool value = true;

            for (int i = 0; i < 64; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    value = false;
                    break;
                }
            }
            return value;
        }

        //����� ��� ���������� �������
        private static void TheadsStart(object sender, DoWorkEventArgs e)
        {
            AddMessage("������ ������, ������ � ������������");

            if (Setting.DeviceModel != DeviceModel.Emulator)
            {
                //vid 2121 pid 2130 � ���������� ������� ����� ��� 8481 � 8496 ��������������
                UsbDeviceFinder myUsbFinder = new UsbDeviceFinder(8481, 8496);

                // ���������� ���������� �����
                _myUsbDevice = UsbDevice.OpenUsbDevice(myUsbFinder);

                if (_myUsbDevice == null)
                {

                    string StringError = "�� ������ ������������ ����������.";
                    _connected = false;

                    AddMessage(StringError);

                    //�������� ������� � ������� �����
                    if (WasDisconnected != null) WasDisconnected(null, new DeviceEventArgsMessage(StringError));

                    return;
                }

                IUsbDevice wholeUsbDevice = _myUsbDevice as IUsbDevice;
                if (!ReferenceEquals(wholeUsbDevice, null))
                {
                    // This is a "whole" USB device. Before it can be used, 
                    // the desired configuration and interface must be selected.

                    // Select config #1
                    wholeUsbDevice.SetConfiguration(1);

                    // Claim interface #0.
                    wholeUsbDevice.ClaimInterface(0);
                }

                // open read endpoint 1.
                _usbReader = _myUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);

                // open write endpoint 1.
                _usbWriter = _myUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep01);

                AddMessage("����������� � �����������, �������");
            }
            else
            {
                AddMessage("...������� ����� ���������...");
            }

            _connected = true;

            AddMessage("����� � ������������ �����������");

            if (WasConnected != null) WasConnected(null);

            // ��� ������������ ���������
            byte[] oldInfoFromController = new byte[64];

            while (_connected)
            {
                // 1. ������� ������ ���� ����
                byte[] readBuffer = new byte[64];
                int bytesRead = 0;

                if (Setting.DeviceModel != DeviceModel.Emulator)
                {
                    _ec = _usbReader.Read(readBuffer, 2000, out bytesRead); 

                     if (_ec != ErrorCode.None)
                    {
                        _connected = false;
                        if (WasDisconnected != null) WasDisconnected(null, new DeviceEventArgsMessage(@"������ ��������� ������ � �����������, ����� ���������!"));
                        
                        return;
                    }
                }
                else
                {
                    //TODO: �������� ��������������� ������ ����������� ������������
                }

                if (bytesRead == 0 || readBuffer[0] != 0x01) continue; //���� �������� ������ ������ � ����� 0�01 

                if (CompareArray(oldInfoFromController, readBuffer)) continue; //���� ������ �� ����������� �� ����������, �� ������ ��� ������...

                deviceInfo.rawData = readBuffer;

                ParseInfo(readBuffer);
                oldInfoFromController = readBuffer;

                if (NewDataFromController != null) NewDataFromController(null);
            }

            if (WasDisconnected != null) WasDisconnected(null, new DeviceEventArgsMessage("")); //������� ���������� ������

            if (Setting.DeviceModel != DeviceModel.Emulator)
            {
                 //���������� ������
                UsbDevice.Exit();
            }

            AddMessage("���������� ������ ������ � ������������");
        }

        /// <summary>
        /// ��������� ����� � ������������
        /// </summary>
        public static void Connect()
        {
            if (Connected)
            {
                AddMessage("���������� ��� �����������!");
                return;
            }

            if (_theads != null)
            {
                if (_theads.IsBusy)
                {
                    AddMessage("��������� ����������� ����������, ���� ������� �� ����� ��������!");
                    return;
                } 
            }

            _connected = false;
            //������� �����, � ���������� � ���� ���������
            _theads = new BackgroundWorker();
            _theads.DoWork += TheadsStart;


            //�������� �����
            _theads.RunWorkerAsync();
        }

        /// <summary>
        /// ���������� �� �����������
        /// </summary>
        public static void Disconnect()
        {
            AddMessage("����������� ����� � ������������!");
            _connected = false;
        }

        #endregion

        #region �������� ������ � ����������

        /// <summary>
        /// ������� � ���������� �������� ������
        /// </summary>
        /// <param name="data">����� ������</param>
        /// <param name="checkBuffSize">��������� �� ������ ���������� ������� �����������</param>
        public static void SendBinaryData(byte[] data, bool checkBuffSize = true)
        {
            if (checkBuffSize && (deviceInfo.FreebuffSize < 2))
            {
                //��� ����� ��������� ���� ����� �� ������������

                //TODO: ����� ����������� ��������� ����� �� ���������....

            }

            // ReSharper disable once SuggestVarOrType_BuiltInTypes
            // ReSharper disable once RedundantAssignment
            int bytesWritten = 64;

            if (Setting.DeviceModel != DeviceModel.Emulator)
            {
               _ec = _usbWriter.Write(data, 2000, out bytesWritten); 
            }
            else
            {
                //TODO: �������� ������� ������������
            }
            
        }

        /// <summary>
        /// ��������� ��������
        /// </summary>
        public static void Spindel_ON()
        {
            SendBinaryData(BinaryData.pack_B5(true));
        }

        /// <summary>
        /// ���������� ��������
        /// </summary>
        public static void Spindel_OFF()
        {
            SendBinaryData(BinaryData.pack_B5(false));
        }

        /// <summary>
        /// ������� ��������� ���������
        /// </summary>
        public static void EnergyStop()
        {
            SendBinaryData(BinaryData.pack_AA(), false);
        }

        /// <summary>
        /// ������ �������� ��� ���������
        /// </summary>
        /// <param name="x">��� � (��������� �������� "+" "0" "-")</param>
        /// <param name="y">��� Y (��������� �������� "+" "0" "-")</param>
        /// <param name="z">��� Z (��������� �������� "+" "0" "-")</param>
        /// <param name="speed"></param>
        public static void StartManualMove(string x, string y, string z, int speed)
        {
            if (!Connected)
            {
                //stringError = "��� ���������� ��������, ����� ������� ���������� ����� � ������������";
                //return false;
                return;
            }

            //if (!IsFreeToTask)
            //{
            //    return;
            //}

            SuperByte axesDirection = new SuperByte(0x00);
            //�������� ������ ����
            if (x == "-") axesDirection.SetBit(0, true);
            if (x == "+") axesDirection.SetBit(1, true);
            if (y == "-") axesDirection.SetBit(2, true);
            if (y == "+") axesDirection.SetBit(3, true);
            if (z == "-") axesDirection.SetBit(4, true);
            if (z == "+") axesDirection.SetBit(5, true);

            //DataClear();
            //DataAdd(BinaryData.pack_BE(axesDirection.valueByte, speed));
            SendBinaryData(BinaryData.pack_BE(axesDirection.ValueByte, speed,x,y,z));
            //Task_Start();
        }

        public static void StopManualMove()
        {
            if (!Connected)
            {
                //stringError = "��� ���������� ��������, ����� ������� ���������� ����� � ������������";
                //return false;
            }

            byte[] buff = BinaryData.pack_BE(0x00, 0);

            //TODO: ����������� ��� ����, ���� ����
            buff[22] = 0x01;

            //DataClear();
            //DataAdd(buff);
            SendBinaryData(buff);
            //Task_Start();
        }

        /// <summary>
        /// ��������� � ����������, ������ ��������� �� ����
        /// </summary>
        /// <param name="x">��������� � ���������</param>
        /// <param name="y">��������� � ���������</param>
        /// <param name="z">��������� � ���������</param>
        public static void DeviceNewPosition(int x, int y, int z)
        {
            if (!TestAllowActions()) return;

            SendBinaryData(BinaryData.pack_C8(x, y, z,0));
        }

        /// <summary>
        /// ��������� � ����������, ������ ��������� �� ���� � �����������
        /// </summary>
        /// <param name="x">� �����������</param>
        /// <param name="y">� �����������</param>
        /// <param name="z">� �����������</param>
        // ReSharper disable once UnusedMember.Global
        public static void DeviceNewPosition(decimal x, decimal y, decimal z)
        {
            if (!TestAllowActions()) return;

            SendBinaryData(BinaryData.pack_C8(deviceInfo.CalcPosPulse("X", x), deviceInfo.CalcPosPulse("Y", y), deviceInfo.CalcPosPulse("Z", z),0));
        }



        #endregion

    }

    /// <summary>
    /// ��������� ��� �������
    /// </summary>
    public class DeviceEventArgsMessage
    {
        protected string _str;

        public string Message
        {
            get { return _str; }
            set { _str = value;}
        }

        public DeviceEventArgsMessage(string Str)
        {
            _str = Str;
        }
    }

    /// <summary>
    /// ������� ������ � �����������
    /// </summary>
    public enum EStatusDevice { Connect = 0, Disconnect };

    public static class deviceInfo
    {
        /// <summary>
        /// ����� ������ �� �����������
        /// </summary>
        public static byte[] rawData = new byte[64];

        /// <summary>
        /// ������ ���������� ������ � �����������
        /// </summary>
        public static byte FreebuffSize = 0;
        /// <summary>
        /// ����� ����������� ����������
        /// </summary>
        public static int NuberCompleatedInstruction = 0;

        /// <summary>
        /// ������� ��������� � ���������
        /// </summary>
        public static int AxesX_PositionPulse = 0;
        /// <summary>
        /// ������� ��������� � ���������
        /// </summary>
        public static int AxesY_PositionPulse = 0;
        /// <summary>
        /// ������� ��������� � ���������
        /// </summary>
        public static int AxesZ_PositionPulse = 0;        
        /// <summary>
        /// ������� ��������� � ���������
        /// </summary>
        public static int AxesA_PositionPulse = 0;

        //public static int AxesX_PulsePerMm = 400;
        //public static int AxesY_PulsePerMm = 400;
        //public static int AxesZ_PulsePerMm = 400;

        //������������ �������
        public static bool AxesX_LimitMax = false;
        public static bool AxesX_LimitMin = false;
        public static bool AxesY_LimitMax = false;
        public static bool AxesY_LimitMin = false;
        public static bool AxesZ_LimitMax = false;
        public static bool AxesZ_LimitMin = false;
        public static bool AxesA_LimitMax = false;
        public static bool AxesA_LimitMin = false;


        public static int shpindel_MoveSpeed = 0;
        public static bool shpindel_Enable = false;

        public static bool Estop = false;


        /// <summary>
        /// ������������� ������������ �����������
        /// </summary>
        public static bool DEMO_DEVICE = false;


        public static decimal AxesX_PositionMM
        {
            get
            {
                return (decimal)AxesX_PositionPulse / Setting.PulseX;
            }
        }

        public static decimal AxesY_PositionMM
        {
            get
            {
                return (decimal)AxesY_PositionPulse / Setting.PulseY;
            }
        }

        public static decimal AxesZ_PositionMM
        {
            get
            {
                return (decimal)AxesZ_PositionPulse / Setting.PulseZ;
            }
        }

        public static decimal AxesA_PositionMM
        {
            get
            {
                return (decimal)AxesA_PositionPulse / Setting.PulseA;
            }
        }

        /// <summary>
        /// ���������� ��������� � ���������, ��� �������� ���, � ��������� � �����������
        /// </summary>
        /// <param name="axes">��� ��� X,Y,Z</param>
        /// <param name="posMm">��������� � ��</param>
        /// <returns>���������� ���������</returns>
        public static int CalcPosPulse(string axes, decimal posMm)
        {
            if (axes == "X") return (int)(posMm * Setting.PulseX);
            if (axes == "Y") return (int)(posMm * Setting.PulseY);
            if (axes == "Z") return (int)(posMm * Setting.PulseZ);
            if (axes == "A") return (int)(posMm * Setting.PulseA);
            return 0;
        }
    }

    /// <summary>
    /// ����� ��� ��������� �������� ������
    /// </summary>
    public static class BinaryData
    {
        /// <summary>
        /// ������ ������� ���� ���������....
        /// </summary>
        /// <param name="byte05"></param>
        /// <returns></returns>
        public static byte[] pack_C0(byte byte05)
        {
            byte[] buf = new byte[64];

            buf[0] = 0xC0;
            buf[5] = byte05;

            return buf;
        }

        public enum TypeSignal
        {
            None,
            Hz,
            RC
        };

        /// <summary>
        /// ���������� ������� ��������
        /// </summary>
        /// <param name="shpindelON">���/��������</param>
        /// <param name="numShimChanel">����� ������ 1,2, ��� 3</param>
        /// <param name="ts">��� �������</param>
        /// <param name="SpeedShim">�������� ������������ ����� �������</param>
        /// <returns></returns>
        public static byte[] pack_B5(bool shpindelON, int numShimChanel = 0, TypeSignal ts = TypeSignal.None, int SpeedShim = 0)
        {
            byte[] buf = new byte[64];

            buf[0] = 0xB5;
            buf[4] = 0x80;


            if (shpindelON)
            {
                buf[5] = 0x02;
            }
            else
            {
                buf[5] = 0x01;
            }

            buf[6] = 0x01; //�.�.

            switch (numShimChanel)
            {
                case 2:
                {
                    buf[8] = 0x02;
                    break;
                }
                case 3:
                {
                    buf[8] = 0x03;
                    break;
                }
                default:
                {
                    buf[8] = 0x00; //�������� ������ 2 � 3 �����, ��������� �� ��������....
                    break;
                }
            }


            switch (ts)
            {
                case TypeSignal.Hz:
                {
                    buf[9] = 0x01;
                    break;
                }

                case  TypeSignal.RC:
                {
                    buf[9] = 0x02;
                    break;
                }
                default:
                {
                    buf[9] = 0x00;
                    break;
                }
            }




            int itmp = SpeedShim;
            buf[10] = (byte)(itmp);
            buf[11] = (byte)(itmp >> 8);
            buf[12] = (byte)(itmp >> 16);


            //buf[10] = 0xFF;
            //buf[11] = 0xFF;
            //buf[12] = 0x04;

            return buf;
        }

        /// <summary>
        /// ��������� ���������
        /// </summary>
        /// <returns></returns>
        public static byte[] pack_AA()
        {
            byte[] buf = new byte[64];
            buf[0] = 0xAA;
            buf[4] = 0x80;
            return buf;
        }

        /// <summary>
        /// ��������� � ���������� ����� ���������, ��� ��������
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public static byte[] pack_C8(int x, int y, int z, int a)
        {
            int newPosX = x;
            int newPosY = y;
            int newPosZ = z;
            int newPosA = a;

            byte[] buf = new byte[64];
            buf[0] = 0xC8;
            //������� ��������� �������
            buf[6] = (byte)(newPosX);
            buf[7] = (byte)(newPosX >> 8);
            buf[8] = (byte)(newPosX >> 16);
            buf[9] = (byte)(newPosX >> 24);
            //������� ��������� �������
            buf[10] = (byte)(newPosY);
            buf[11] = (byte)(newPosY >> 8);
            buf[12] = (byte)(newPosY >> 16);
            buf[13] = (byte)(newPosY >> 24);
            //������� ��������� �������
            buf[14] = (byte)(newPosZ);
            buf[15] = (byte)(newPosZ >> 8);
            buf[16] = (byte)(newPosZ >> 16);
            buf[17] = (byte)(newPosZ >> 24);       
            //������� ��������� �������
            buf[18] = (byte)(newPosA);
            buf[19] = (byte)(newPosA >> 8);
            buf[20] = (byte)(newPosA >> 16);
            buf[21] = (byte)(newPosA >> 24);

            return buf;
        }










        /// <summary>
        /// �������� ����� �����������, ��� ������������
        /// </summary>
        /// <returns></returns>
        public static byte[] pack_D2(int speed, decimal returnDistance)
        {
            byte[] buf = new byte[64];

            buf[0] = 0xD2;


            int inewSpd = 0;

            if (speed != 0)
            {
                double dnewSpd = (1800 / (double)speed) * 1000;
                inewSpd = (int)dnewSpd;
            }
            //��������
            buf[43] = (byte)(inewSpd);
            buf[44] = (byte)(inewSpd >> 8);
            buf[45] = (byte)(inewSpd >> 16);


            //�.�.
            buf[46] = 0x10;

            // 
            int inewReturn = (int)(returnDistance * Setting.PulseZ);

            //��������� ��������
            buf[50] = (byte)(inewReturn);
            buf[51] = (byte)(inewReturn >> 8);
            buf[52] = (byte)(inewReturn >> 16);
            
            //�.�.
            buf[55] = 0x12;
            buf[56] = 0x7A;

            return buf;
        }



        /// <summary>
        /// ������ �������� ��� ��������� (� ���������)
        /// </summary>
        /// <param name="direction">����������� �� ���� � �����</param>
        /// <param name="speed">�������� ��������</param>
        /// <returns></returns>
        public static byte[] pack_BE(byte direction, int speed, string x = "_", string y = "_", string z = "_", string a = "_")
        {
            //TODO: ���������� ����������� � ������������� ��������

            byte[] buf = new byte[64];

            buf[0] = 0xBE;
            buf[4] = 0x80;
            buf[6] = direction;

            int inewSpd = 0;

            if (speed != 0)
            {
                double dnewSpd = (1800 / (double)speed) * 1000;
                inewSpd = (int)dnewSpd;
            }

            //��������
            buf[10] = (byte)(inewSpd);
            buf[11] = (byte)(inewSpd >> 8);
            buf[12] = (byte)(inewSpd >> 16);

            if (Setting.DeviceModel == DeviceModel.MK2)
            {
                //TODO: ��� ��2 ������� ���� ������� ������

                if (speed != 0)
                {
                    double dnewSpd = (9000 / (double)speed) * 1000;
                    inewSpd = (int)dnewSpd;
                }

                //��������
                buf[10] = (byte)(inewSpd);
                buf[11] = (byte)(inewSpd >> 8);
                buf[12] = (byte)(inewSpd >> 16);

                if (speed == 0)
                {
                    buf[14] = 0x00;
                    buf[18] = 0x01;
                    buf[22] = 0x01;

                    //x
                    buf[26] = 0x00;
                    buf[27] = 0x00;
                    buf[28] = 0x00;
                    buf[29] = 0x00;

                    //y
                    buf[30] = 0x00;
                    buf[31] = 0x00;
                    buf[32] = 0x00;
                    buf[33] = 0x00;

                    //z
                    buf[34] = 0x00;
                    buf[35] = 0x00;
                    buf[36] = 0x00;
                    buf[37] = 0x00;

                    //a
                    buf[38] = 0x00;
                    buf[39] = 0x00;
                    buf[40] = 0x00;
                    buf[41] = 0x00;


                }
                else
                {
                    buf[14] = 0xC8; //TODO: WTF?? 
                    buf[18] = 0x14; //TODO: WTF??
                    buf[22] = 0x14; //TODO: WTF??




                    if (x == "+")
                    {
                        buf[26] = 0x40;
                        buf[27] = 0x0D;
                        buf[28] = 0x03;
                        buf[29] = 0x00;
                    }

                    if (x == "-")
                    {
                        buf[26] = 0xC0;
                        buf[27] = 0xF2;
                        buf[28] = 0xFC;
                        buf[29] = 0xFF;
                    }

                    if (y == "+")
                    {
                        buf[30] = 0x40;
                        buf[31] = 0x0D;
                        buf[32] = 0x03;
                        buf[33] = 0x00;
                    }

                    if (y == "-")
                    {
                        buf[30] = 0xC0;
                        buf[31] = 0xF2;
                        buf[32] = 0xFC;
                        buf[33] = 0xFF;
                    }

                    if (z == "+")
                    {
                        buf[34] = 0x40;
                        buf[35] = 0x0D;
                        buf[36] = 0x03;
                        buf[37] = 0x00;
                    }

                    if (z == "-")
                    {
                        buf[34] = 0xC0;
                        buf[35] = 0xF2;
                        buf[36] = 0xFC;
                        buf[37] = 0xFF;
                    }

                    if (a == "+")
                    {
                        buf[38] = 0x40;
                        buf[39] = 0x0D;
                        buf[40] = 0x03;
                        buf[41] = 0x00;
                    }

                    if (a == "-")
                    {
                        buf[38] = 0xC0;
                        buf[39] = 0xF2;
                        buf[40] = 0xFC;
                        buf[41] = 0xFF;
                    }



                }
            }

            


            return buf;
        }



        ///// <summary>
        ///// ������������ �������� ��� ������������
        ///// </summary>
        ///// <returns></returns>
        //public static byte[] pack_CA()
        //{
        //    byte[] buf = new byte[64];


        //    buf[0] = 0xCA;

        //    buf[5] = 0xB9;
        //    buf[14] = 0xD0;
        //    buf[15] = 0x07;
        //    buf[43] = 0x10;
        //    buf[44] = 0x0E;

        //    return buf;
        //}















        public static byte[] pack_9E(byte value)
        {
            byte[] buf = new byte[64];

            buf[0] = 0x9e;
            buf[5] = value;

            return buf;
        }

        /// <summary>
        /// ��������� ����������� ��������
        /// </summary>
        /// <param name="speedLimitX">������������ �������� �� ��� X</param>
        /// <param name="speedLimitY">������������ �������� �� ��� Y</param>
        /// <param name="speedLimitZ">������������ �������� �� ��� Z</param>
        /// <returns></returns>
        public static byte[] pack_BF(int speedLimitX, int speedLimitY, int speedLimitZ, int speedLimitA)
        {
            byte[] buf = new byte[64];

            buf[0] = 0xbf;

            buf[4] = 0x00;

            double koef = 4500;

            if (Setting.DeviceModel == DeviceModel.MK1)
            {
                buf[4] = 0x80; //TODO: ���������� ����
                koef = 3600;
            }

            if (Setting.DeviceModel == DeviceModel.MK2)
            {
                buf[4] = 0x00; //TODO: ���������� ����
                koef = 4500;
            }


            double dnewSpdX = (koef / (double)speedLimitX) * 1000;
            int inewSpdX = (int)dnewSpdX;

            double dnewSpdY = (koef / (double)speedLimitY) * 1000;
            int inewSpdY = (int)dnewSpdY;

            double dnewSpdZ = (koef / (double)speedLimitZ) * 1000;
            int inewSpdZ = (int)dnewSpdZ;

            double dnewSpdA = (koef / (double)speedLimitA) * 1000;
            int inewSpdA = (int)dnewSpdA;

            buf[07] = (byte)(inewSpdX);
            buf[08] = (byte)(inewSpdX >> 8);
            buf[09] = (byte)(inewSpdX >> 16);
            buf[10] = (byte)(inewSpdX >> 24);


            buf[11] = (byte)(inewSpdY);
            buf[12] = (byte)(inewSpdY >> 8);
            buf[13] = (byte)(inewSpdY >> 16);
            buf[14] = (byte)(inewSpdY >> 24);

            buf[15] = (byte)(inewSpdZ);
            buf[16] = (byte)(inewSpdZ >> 8);
            buf[17] = (byte)(inewSpdZ >> 16);
            buf[18] = (byte)(inewSpdZ >> 24);

            buf[19] = (byte)(inewSpdA);
            buf[20] = (byte)(inewSpdA >> 8);
            buf[21] = (byte)(inewSpdA >> 16);
            buf[22] = (byte)(inewSpdA >> 24);

            return buf;
        }

        /// <summary>
        /// ����������� �������
        /// </summary>
        /// <returns></returns>
        public static byte[] pack_C0()
        {
            byte[] buf = new byte[64];

            buf[0] = 0xc0;

            return buf;
        }

        /// <summary>
        /// �������� � ��������� �����
        /// </summary>
        /// <param name="_posX">��������� X � ���������</param>
        /// <param name="_posY">��������� Y � ���������</param>
        /// <param name="_posZ">��������� Z � ���������</param>
        /// <param name="_speed">�������� ��/������</param>
        /// <param name="_NumberInstruction">����� ������ ����������</param>
        /// <param name="AngleVectors">����, �� ������� ���������� ����������� ��������</param>
        /// <param name="Distance">����� ������� ������� � ��</param>
        /// <returns>����� ������ ��� �������</returns>
        public static byte[] pack_CA(int _posX, int _posY, int _posZ, int _posA, int _speed, int _NumberInstruction, int AngleVectors, decimal Distance, int _valuePause = 0x39)
        {
            int newPosX = _posX;
            int newPosY = _posY;
            int newPosZ = _posZ;
            int newPosA = _posA;
            int newInst = _NumberInstruction;

            byte[] buf = new byte[64];

            buf[0] = 0xCA;
            //������ ������ ����������
            buf[1] = (byte)(newInst);
            buf[2] = (byte)(newInst >> 8);
            buf[3] = (byte)(newInst >> 16);
            buf[4] = (byte)(newInst >> 24);

            // ���� ���� ����� 2-�� ���������, �������� ����������� ����� ��������, �� ������� � �������
            //int deltaAngle = 180 - AngleVectors;

            //buf[5] = 0x01;

            //if (deltaAngle > 45) buf[5] = 0x39;


            //if (deltaAngle <= 25) 
            //buf[5] = 0x03;
            buf[5] = (byte)_valuePause;


            if (Distance >0 && Distance < 5) buf[5] = 0x03;

            //if (deltaAngle < 15) buf[5] = 0x10;


            //if (deltaAngle < 10) buf[5] = 0x02;


            //if (deltaAngle < 3) buf[5] = 0x01;



            //buf[5] = (byte)deltaAngle;

            //if (buf[5] == 0x00) buf[5] = 0x01;

            //if (buf[5] > 0x39) buf[5] = 0x39;

            //TODO: ������ �������� ��� ��������
            //// 0�01 ��� �����, 0�39 ���� ����� (��� ��������� �����, � ������� �������� ���������� ����...)
            //if (AngleVectors > 170 && AngleVectors < 190)
            //{
            //    buf[5] = 0x01;
            //}
            //else
            //{
            //    buf[5] = 0x39;
            //}

            //������� ��������� �������
            buf[6] = (byte)(newPosX);
            buf[7] = (byte)(newPosX >> 8);
            buf[8] = (byte)(newPosX >> 16);
            buf[9] = (byte)(newPosX >> 24);

            //������� ��������� �������
            buf[10] = (byte)(newPosY);
            buf[11] = (byte)(newPosY >> 8);
            buf[12] = (byte)(newPosY >> 16);
            buf[13] = (byte)(newPosY >> 24);

            //������� ��������� �������
            buf[14] = (byte)(newPosZ);
            buf[15] = (byte)(newPosZ >> 8);
            buf[16] = (byte)(newPosZ >> 16);
            buf[17] = (byte)(newPosZ >> 24);

            //������� ��������� �������
            buf[18] = (byte)(newPosA);
            buf[19] = (byte)(newPosA >> 8);
            buf[20] = (byte)(newPosA >> 16);
            buf[21] = (byte)(newPosA >> 24);



            double koef = 4500;

            if (Setting.DeviceModel == DeviceModel.MK1)
            {
                buf[4] = 0x80; //TODO: ���������� ����
                koef = 3600;
            }

            if (Setting.DeviceModel == DeviceModel.MK2)
            {
                buf[4] = 0x00; //TODO: ���������� ����
                koef = 4500;
            }


            //TODO: ���� ��������� = 0 �� ���������� ��������....

            //TODO: ������ ����� ����������, ���� ��� �������� �� ������ ������� ������� ��������, �.�. ����� ����� ������� �������� 
            //int SpeedToSend = _speed;

            int SpeedToSend = 2328; 
            //������ ���
            if (_speed != 0)
            {
                SpeedToSend = _speed;
            }

            //TODO: �������� ����������� �������� �� ���������!!!
            //��� ������� ��������� ���������� �������� �������� G-�����
            //if (Distance > 50) SpeedToSend = _speed;
            //else
            //{
            //    //����� �������� ��������
            //    /* ��������� 50�� �������� = 500��/������
            //     * ��������� 30�� �������� = 300��/������
            //     * ��������� 10�� �������� = 100��/������
            //     * � �.�....
            //     */
            //    //SpeedToSend = Distance * 10;
            //    SpeedToSend = 500;
            //    // �� ������ ����� ��������� 
            //    //if (Distance == 0) SpeedToSend = 200;
            //}

            //////if (Distance < 10) SpeedToSend = 400;

            //////if (_speed == 0) SpeedToSend = 200;



            int iSpeed = (int)(koef / SpeedToSend) * 1000;
            //�������� ��� �
            buf[43] = (byte)(iSpeed);
            buf[44] = (byte)(iSpeed >> 8);
            buf[45] = (byte)(iSpeed >> 16);
            
            buf[54] = 0x40;  //TODO: ���������� ����

            return buf;
        }

        /// <summary>
        /// ���������� ���������� ���� ��������
        /// </summary>
        /// <returns></returns>
        public static byte[] pack_FF()
        {
            byte[] buf = new byte[64];

            buf[0] = 0xff;

            return buf;
        }

        /// <summary>
        /// ����������� �������
        /// </summary>
        /// <returns></returns>
        public static byte[] pack_9D()
        {
            byte[] buf = new byte[64];

            buf[0] = 0x9d;

            return buf;
        }

        //public static byte[] GetPack07()
        //{
        //    byte[] buf = new byte[64];

        //    buf[0] = 0x9E;
        //    buf[5] = 0x02;

        //    return buf;
        //}





    }

    #region ����� ������

    /// <summary>
    /// �������� ������ ������ G-kode, � ��� ������ 3d
    /// </summary>
    public static class dataCode
    {
        ///// <summary>
        ///// ����� ������� ���������� ��� ������ 
        ///// </summary>
        //public static List<GKOD_ready> GKODready = new List<GKOD_ready>();

        /// <summary>
        /// ����� ����� ���������� ��� ������ 
        /// </summary>
        //public static List<GKOD_raw> GKODraw = new List<GKOD_raw>();

        /// <summary>
        /// ����� ����� �������, ���������� ��� ������������ �����������
        /// </summary>
        //public static List<matrixYline> Matrix = new List<matrixYline>(); 


        ///// <summary>
        ///// ������� ������
        ///// </summary>
        //public static void Clear()
        //{
        //    //GKODready.Clear();
        //    //GKODraw.Clear();
        //}




        //����� ������� �������
        public static dobPoint[,] matrix2 = new dobPoint[1,1]; 


    
    
    
    }






    public class matrixYline
    {
        public decimal Y = 0;       // ���������� � ��
        public List<matrixPoint> X = new List<matrixPoint>(); //����� ��������� �� ��� X, � �������� ������������ �� ��� �����
    }

    public class matrixPoint
    {
        public decimal X = 0;       // ���������� � ��
        public decimal Z = 0;       // ���������� � ��
        public bool Used = false;       // ������������ �� ��� �����

        public matrixPoint(decimal _X, decimal _Z, bool _Used)
        {
            X = _X;
            Z = _Z;
            Used = _Used;
        }
    }






    #endregion

    public class decPoint
    {

        public decimal X;       // ���������� � ��
        public decimal Y;       // ���������� � ��
        public decimal Z;       // ���������� � ��
        public decimal A;       // ���������� � ��

        public decPoint(decimal _x, decimal _y, decimal _z, decimal _a)
        {
            X = _x;
            Y = _y;
            Z = _z;
            A = _a;
        }
    }

    public class dobPoint
    {

        public double X;       // ���������� � ��
        public double Y;       // ���������� � ��
        public double Z;       // ���������� � ��
        public double A;       // ���������� � ��

        public dobPoint(double _x, double _y, double _z, double _a)
        {
            X = _x;
            Y = _y;
            Z = _z;
            A = _a;
        }
    }


    //����� ��� ������ � ����������
    public static class Geometry
    {
        /*
         *    ������������� ������ �� ��� Z, � ����� �5, ���� ������ �� Z � ����� 1,2,3,4 
         * 
         *  /\ ��� Y
         *  |
         *  |    (����� �1) -------------*--------------- (����� �2)
         *  |                            |
         *  |                            |
         *  |                            |
         *  |                       (����� �5)
         *  |                            |
         *  |                            |
         *  |    (����� �3) -------------*--------------- (����� �4)
         *  |
         *  |
         *  *----------------------------------------------------------------> ��� X 
         *  ������������� ����������� ��������� �������:
         *  1) ���� ���������� X � ����� 5, � ���������� ����� 1 � 2, ��������� ������ Z � ����� ������� ��������� �� ����� ����� 1,2 � ��������������� 5-� ����� (�������� ����� �12)
         *  2) ���� ����� ����������� ��� ����� �� ����� ����� 3,4 (�������� ����� �34)
         *  3) ���� ���������� ����� �12, �34 � �������� �� ��� Y � ����� 5, ��������� ������ �� ��� Z 
         */


        /// <summary>
        /// ������� ������������ ������ �� ��� Z
        /// </summary>
        /// <param name="p1">������ ����� ������ ����� X</param>
        /// <param name="p2">������ ����� ������ ����� X</param>
        /// <param name="p3">������ ����� ������ ����� X</param>
        /// <param name="p4">������ ����� ������ ����� X</param>
        /// <param name="p5">����� � ������� ����� ��������������� ������</param>
        /// <returns></returns>
        public static dobPoint GetZ(dobPoint p1, dobPoint p2, dobPoint p3, dobPoint p4, dobPoint p5)
        {
            dobPoint p12 = CalcPX(p1, p2, p5);
            dobPoint p34 = CalcPX(p3, p4, p5);

            dobPoint p1234 = CalcPY(p12, p34, p5);

            return p1234;
        }

        //���������� ������ Z ����� p0, ������� �� ������ ������� ���������� ��� X
        public static dobPoint CalcPX(dobPoint p1, dobPoint p2, dobPoint p0)
        {
            dobPoint ReturnPoint = new dobPoint(p0.X, p0.Y, p0.Z,0);

            ReturnPoint.Z = p1.Z + (((p1.Z - p2.Z) / (p1.X - p2.X)) * (p0.X - p1.X));

            //TODO: ������ �� ������� ��� ����� 1 � 2 ����� ������ �� �� ����� ���������� ����� ��� �
            ReturnPoint.Y = p1.Y;

            return ReturnPoint;
        }



        //TODO: ������� �� ����
        //���������� ������ Z ����� p0, ������� �� ������ ����� ������� p3 p4  (������ ���������� ��� Y)
        public static dobPoint CalcPY(dobPoint p1, dobPoint p2, dobPoint p0)
        {
            dobPoint ReturnPoint = new dobPoint(p0.X, p0.Y, p0.Z,0);

            ReturnPoint.Z = p1.Z + (((p1.Z - p2.Z) / (p1.Y - p2.Y)) * (p0.Y - p1.Y));

            return ReturnPoint;
        }

    }
}
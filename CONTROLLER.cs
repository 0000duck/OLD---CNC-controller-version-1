using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace CNC_Controller
{
    /// <summary>
    /// ����� ������ � ������������ MK1
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class CONTROLLER
    {
        #region ������������� ������� �����������

        public delegate void DeviceEventConnect(object sender); //����������� �� ��������� �����
        public delegate void DeviceEventDisconnect(object sender, DeviceEventArgsMessage e); //����������� �� ������/����������� �����
        public delegate void DeviceEventNewData(object sender); //����������� ��� �������� ����� ������ ������������
        public delegate void DeviceEventNewMessage(object sender, DeviceEventArgsMessage e); //��� ������� ����������� ��������� ��������� � ���������

        /// <summary>
        /// ������� ��� �������� ����������� � �����������
        /// </summary>
        public event DeviceEventConnect WasConnected;
        /// <summary>
        /// ������� ��� ���������� �� �����������, ��� ������� ����� � ������������
        /// </summary>
        public event DeviceEventDisconnect WasDisconnected;
        /// <summary>
        /// �������� ����� ������ �� �����������
        /// </summary>
        public event DeviceEventNewData NewDataFromController;
        /// <summary>
        /// ������� ������ �������� (��� ������� �����)
        /// </summary>
        public event DeviceEventNewMessage Message;

        #endregion
        
        #region ���������� ���������

        /// <summary>
        /// ������� ����� � ������������
        /// </summary>
        private bool _connected;

        /// <summary>
        /// ����� ��� ���������, ������� ������ � ����������
        /// </summary>
        private BackgroundWorker _theads;

        private UsbDevice _myUsbDevice;
        private ErrorCode _ec;
        private UsbEndpointReader _usbReader;
        private UsbEndpointWriter _usbWriter;

        #endregion

        /// <summary>
        /// ����������� ������
        /// </summary>
        public CONTROLLER()
        {
            _connected = false;
            //������� �����, � ���������� � ���� ���������
            _theads = new BackgroundWorker();
            _theads.DoWork += TheadsStart;
        }

        #region ������ � ����������� ���������

        /// <summary>
        /// ���� �������� ���������
        /// </summary>
        private readonly string _filesetting = string.Format("{0}\\setting.ini", Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));

        /// <summary>
        /// ������� ��� ���������� ���������� � ����� ��������
        /// ����������� ������ � ����� ����� ��������� ��������� �������:
        /// �������� = ��������
        /// !!! ������� ���� ����� ������ ������������ �� � ��������� �� � ��������
        /// </summary>
        /// <param name="property">��� ��������� (������)</param>
        /// <param name="value">�������� ��������� (������)</param>
        private void SaveProperty(string property, string value)
        {
            List<string> listProperty = new List<string>();

            // ������ ��������� � ����

            // � ������ ������� ��� ���������
            if (File.Exists(_filesetting))
            {

                StreamReader sr = new StreamReader(_filesetting);
                string[] arr = sr.ReadToEnd().Split('\n');
                sr.Close();

                foreach (string ss in arr)
                {
                    // ReSharper disable once RedundantAssignment
                    var sformat = ss.Replace('\n', ' ').Trim();
                    sformat = ss.Replace('\r', ' ').Trim();

                    if (sformat.Length < 3) continue;
                    //�������� ��� �� ��� ��������
                    int posSymbol = sformat.IndexOf('=');

                    if (posSymbol == 0) continue; //�������� ��������, ����� �� ����� ���

                    string sProperty = sformat.Substring(0, posSymbol);
                    string sValue = sformat.Substring(posSymbol + 1);

                    if (property.Trim() == sProperty.Trim()) continue; //������ �������� ���������

                    listProperty.Add(sProperty + "=" + sValue);
                }
            }

            //���� � ������������ ����� ������ ��������� ���, �� ������� �����
            listProperty.Add(property + "=" + value);

            try
            {
                string sOut = "";

                foreach (string ss in listProperty)
                {
                    sOut += ss + Environment.NewLine;

                    //OutputFile.WriteLine(ss);
                }

                StreamWriter sw = new StreamWriter(_filesetting);
                sw.WriteLine(sOut);
                sw.Close();
            }
            catch (Exception)
            {
                //addLog(e.ToString(), true);
            }
        }

        /// <summary>
        /// ������� ���������� ��������� �� ����� ��������
        /// </summary>
        /// <param name="property">��� ��������� (������)</param>
        /// <returns>�������� ��������� (������), ���� ����� ������������� ���� ��������, ��� ��������� ��������, �� �������� ""</returns>
        private string LoadProperty(string property)
        {
            if (!File.Exists(_filesetting)) return "";
            var sr = new StreamReader(_filesetting);
            var arr = sr.ReadToEnd().Split('\n');
            sr.Close();

            foreach (var ss in arr)
            {
                //�������� ��� �� ��� ��������
                var posSymbol = ss.IndexOf('=');

                if (posSymbol == 0) continue; //�������� ��������, ����� �� ����� ���

                var sProperty = ss.Substring(0, posSymbol);
                var sValue = ss.Substring(posSymbol + 1);

                if (property.Trim() == sProperty.Trim())
                {
                    return sValue;
                }
            }
            return "";
        }

        /// <summary>
        /// �������� �������� ��������� �� �����
        /// </summary>
        public void LoadSetting()
        {
            string sPulseX = LoadProperty("pulseX");
            string sPulseY = LoadProperty("pulseY");
            string sPulseZ = LoadProperty("pulseZ");

            if (sPulseX.Trim() != "") deviceInfo.AxesX_PulsePerMm = int.Parse(sPulseX);
            if (sPulseY.Trim() != "") deviceInfo.AxesY_PulsePerMm = int.Parse(sPulseY);
            if (sPulseZ.Trim() != "") deviceInfo.AxesZ_PulsePerMm = int.Parse(sPulseZ);

        }

        /// <summary>
        /// ���������� �������� � ����
        /// </summary>
        public void SaveSetting()
        {
            SaveProperty("pulseX", deviceInfo.AxesX_PulsePerMm.ToString());
            SaveProperty("pulseY", deviceInfo.AxesY_PulsePerMm.ToString());
            SaveProperty("pulseZ", deviceInfo.AxesZ_PulsePerMm.ToString());
        }

        #endregion

        #region �������� ��� ������� ����� � ����������
        //TODO: ������������ ������������� ��������
        /// <summary>
        /// ���������� ���������� � ������� �����
        /// </summary>
        public bool Connected
        {
            get
            {
                return _connected;
            }
        }

        /// <summary>
        /// �������� �������� ��������
        /// </summary>
        public int ShpindelMoveSpeed
        {
            get
            {
                return deviceInfo.shpindel_MoveSpeed;
            }
        }

        /// <summary>
        /// ����� ����������� ����������
        /// </summary>
        public int NumberComleatedInstructions
        {
            get
            {
                return deviceInfo.NuberCompleatedInstruction;
            }
        }

        /// <summary>
        /// �������� ������� �� ��������
        /// </summary>
        public bool SpindelOn
        {
            get { return deviceInfo.shpindel_Enable; }
        }

        /// <summary>
        /// �������� ������������� �� ��������� ���������
        /// </summary>
        public bool EstopOn
        {
            get { return deviceInfo.Estop; }
        }

        /// <summary>
        /// �������� ������� �����, � ����������� �����������
        /// </summary>
        /// <returns>������, �������� �� �������� ����������� ������</returns>
        public bool TestAllowActions()
        {
            if (!Connected)
            {
                //StringError = @"����������� ����� � ������������!";
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��� �������� ����� ������ �� �����������
        /// </summary>
        public bool AvailableNewData { get; set; }


        /// <summary>
        /// ������ ���������� ������
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public int AvailableBufferSize
        {
            get
            {
                return deviceInfo.FreebuffSize;
            }
            // ReSharper disable once ValueParameterNotUsed
            set
            {
            }
        }

        #endregion

        #region ����� ���������� �������

        /// <summary>
        /// ������ ���������� ������ � �����������
        /// </summary>
        /// <param name="readBuffer"></param>
        private void ParseInfo(IList<byte> readBuffer)
        {
            deviceInfo.FreebuffSize = readBuffer[1];
            deviceInfo.shpindel_MoveSpeed = (int)(((readBuffer[22] * 65536) + (readBuffer[21] * 256) + (readBuffer[20])) / 2.1); 
            deviceInfo.AxesX_PositionPulse = (readBuffer[27] * 16777216) + (readBuffer[26] * 65536) + (readBuffer[25] * 256) + (readBuffer[24]);
            deviceInfo.AxesY_PositionPulse = (readBuffer[31] * 16777216) + (readBuffer[30] * 65536) + (readBuffer[29] * 256) + (readBuffer[28]);
            deviceInfo.AxesZ_PositionPulse = (readBuffer[35] * 16777216) + (readBuffer[34] * 65536) + (readBuffer[33] * 256) + (readBuffer[32]);

            deviceInfo.AxesX_LimitMax = (readBuffer[15] & (1 << 0)) != 0;
            deviceInfo.AxesX_LimitMin = (readBuffer[15] & (1 << 1)) != 0;
            deviceInfo.AxesY_LimitMax = (readBuffer[15] & (1 << 2)) != 0;
            deviceInfo.AxesY_LimitMin = (readBuffer[15] & (1 << 3)) != 0;
            deviceInfo.AxesZ_LimitMax = (readBuffer[15] & (1 << 4)) != 0;
            deviceInfo.AxesZ_LimitMin = (readBuffer[15] & (1 << 5)) != 0;

            deviceInfo.NuberCompleatedInstruction = (int)(readBuffer[9] * 4294967296) + (readBuffer[8] * 65536) + (readBuffer[7] * 256) + (readBuffer[6]);

            SuperByte bb = new SuperByte(readBuffer[19]);

            deviceInfo.shpindel_Enable = bb.Bit0;

            SuperByte bb2 = new SuperByte(readBuffer[14]);
            deviceInfo.Estop = bb2.Bit7;
        }

        private void ADDMessage(string ss)
        {
            if (Message != null) Message(this, new DeviceEventArgsMessage(ss));
        }

        private bool CompareArray(byte[] arr1, byte[] arr2)
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
        private void TheadsStart(object sender, DoWorkEventArgs e)
        {
            ADDMessage("������ ������, ������ � ������������");

            if (!deviceInfo.DEMO_DEVICE)
            {
                //vid 2121 pid 2130 � ���������� ������� ����� ��� 8481 � 8496 ��������������
                UsbDeviceFinder myUsbFinder = new UsbDeviceFinder(8481, 8496);

                // ���������� ���������� �����
                _myUsbDevice = UsbDevice.OpenUsbDevice(myUsbFinder);

                if (_myUsbDevice == null)
                {

                    string StringError = "�� ������ ������������ ����������.";
                    _connected = false;

                    ADDMessage(StringError);

                    //�������� ������� � ������� �����
                    if (WasDisconnected != null) WasDisconnected(this, new DeviceEventArgsMessage(StringError));

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

                ADDMessage("����������� � �����������, �������");
            }
            else
            {
                ADDMessage("...������� ���������...");
            }

            AvailableNewData = true;
            _connected = true;

            ADDMessage("����� � ������������ �����������");

            if (WasConnected != null) WasConnected(this);

            // ��� ������������ ���������
            byte[] _oldInfoFromController = new byte[64];

            while (_connected)
            {
                // 1. ������� ������ ���� ����
                byte[] readBuffer = new byte[64];
                int bytesRead = 0;

                if (!deviceInfo.DEMO_DEVICE)
                {
                    _ec = _usbReader.Read(readBuffer, 2000, out bytesRead); 

                     if (_ec != ErrorCode.None)
                    {
                        _connected = false;
                        if (WasDisconnected != null) WasDisconnected(this, new DeviceEventArgsMessage(@"������ ��������� ������ � �����������, ����� ���������!"));
                        
                        return;
                    }
                }
                else
                {
                    //TODO: �������� ��������������� ������ ����������� ������������
                }

                if (bytesRead == 0 || readBuffer[0] != 0x01) continue; //���� �������� ������ ������ � ����� 0�01 

                if (CompareArray(_oldInfoFromController, readBuffer)) continue; //���� ������ �� ����������� �� ����������, �� ������ ��� ������...

                deviceInfo.rawData = readBuffer;

                ParseInfo(readBuffer);
                _oldInfoFromController = readBuffer;
                AvailableNewData = true;

                if (NewDataFromController != null) NewDataFromController(this);
            }

            if (WasDisconnected != null) WasDisconnected(this, new DeviceEventArgsMessage("")); //������� ���������� ������

            if (!deviceInfo.DEMO_DEVICE)
            {
                 //���������� ������
                UsbDevice.Exit();
            }

            ADDMessage("���������� ������ ������ � ������������");
        }

        /// <summary>
        /// ��������� ����� � ������������
        /// </summary>
        public void Connect()
        {
            if (Connected)
            {
                ADDMessage("���������� ��� �����������!");
                return;
            }

            if (_theads.IsBusy)
            {
                ADDMessage("��������� ����������� ����������, ���� ������� �� ����� ��������!");
                return;
            }

            //�������� �����
            _theads.RunWorkerAsync();
        }

        /// <summary>
        /// ���������� �� �����������
        /// </summary>
        public void Disconnect()
        {
            ADDMessage("����������� ����� � ������������!");
            _connected = false;
        }

        #endregion

        #region �������� ������ � ����������

        /// <summary>
        /// ������� � ���������� �������� ������
        /// </summary>
        /// <param name="data">����� ������</param>
        /// <param name="checkBuffSize">��������� �� ������ ���������� ������� �����������</param>
        public void SendBinaryData(byte[] data, bool checkBuffSize = true)
        {
            if (checkBuffSize && (deviceInfo.FreebuffSize < 2))
            {
                //��� ����� ��������� ���� ����� �� ������������

                //TODO: ����� ����������� ��������� ����� �� ���������....

            }

            // ReSharper disable once SuggestVarOrType_BuiltInTypes
            // ReSharper disable once RedundantAssignment
            int bytesWritten = 64;

            if (!deviceInfo.DEMO_DEVICE)
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
        public void Spindel_ON()
        {
            SendBinaryData(BinaryData.pack_B5(true));
        }

        /// <summary>
        /// ���������� ��������
        /// </summary>
        public void Spindel_OFF()
        {
            SendBinaryData(BinaryData.pack_B5(false));
        }

        /// <summary>
        /// ������� ��������� ���������
        /// </summary>
        public void EnergyStop()
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
        public void StartManualMove(string x, string y, string z, int speed)
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
            SendBinaryData(BinaryData.pack_BE(axesDirection.ValueByte, speed));
            //Task_Start();
        }

        public void StopManualMove()
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
        public void DeviceNewPosition(int x, int y, int z)
        {
            if (!TestAllowActions()) return;

            SendBinaryData(BinaryData.pack_C8(x, y, z));
        }

        /// <summary>
        /// ��������� � ����������, ������ ��������� �� ���� � �����������
        /// </summary>
        /// <param name="x">� �����������</param>
        /// <param name="y">� �����������</param>
        /// <param name="z">� �����������</param>
        // ReSharper disable once UnusedMember.Global
        public void DeviceNewPosition(decimal x, decimal y, decimal z)
        {
            if (!TestAllowActions()) return;

            SendBinaryData(BinaryData.pack_C8(deviceInfo.CalcPosPulse("X", x), deviceInfo.CalcPosPulse("Y", y), deviceInfo.CalcPosPulse("Z", z)));
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



    static class deviceInfo
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

        public static int AxesX_PulsePerMm = 400;
        public static int AxesY_PulsePerMm = 400;
        public static int AxesZ_PulsePerMm = 400;

        //������������ �������
        public static bool AxesX_LimitMax = false;
        public static bool AxesX_LimitMin = false;
        public static bool AxesY_LimitMax = false;
        public static bool AxesY_LimitMin = false;
        public static bool AxesZ_LimitMax = false;
        public static bool AxesZ_LimitMin = false;


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
                return (decimal)AxesX_PositionPulse / AxesX_PulsePerMm; ;
            }
        }

        public static decimal AxesY_PositionMM
        {
            get
            {
                return (decimal)AxesY_PositionPulse / AxesY_PulsePerMm; ;
            }
        }

        public static decimal AxesZ_PositionMM
        {
            get
            {
                return (decimal)AxesZ_PositionPulse / AxesZ_PulsePerMm; ;
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
            if (axes == "X") return (int)(posMm * (decimal)AxesX_PulsePerMm);
            if (axes == "Y") return (int)(posMm * (decimal)AxesY_PulsePerMm);
            if (axes == "Z") return (int)(posMm * (decimal)AxesZ_PulsePerMm);
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
        public static byte[] pack_C8(int x, int y, int z)
        {
            int newPosX = x;
            int newPosY = y;
            int newPosZ = z;

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
            int inewReturn = (int)(returnDistance * (decimal)deviceInfo.AxesZ_PulsePerMm);

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
        public static byte[] pack_BE(byte direction, int speed)
        {
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
        public static byte[] pack_BF(int speedLimitX, int speedLimitY, int speedLimitZ)
        {
            byte[] buf = new byte[64];

            buf[0] = 0xbf;
            buf[4] = 0x80; //TODO: ���������� ����


            double dnewSpdX = (3600 / (double)speedLimitX) * 1000;
            int inewSpdX = (int)dnewSpdX;

            double dnewSpdY = (3600 / (double)speedLimitY) * 1000;
            int inewSpdY = (int)dnewSpdY;

            double dnewSpdZ = (3600 / (double)speedLimitZ) * 1000;
            int inewSpdZ = (int)dnewSpdZ;

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
        /// <returns>����� ������ ��� �������</returns>
        public static byte[] pack_CA(int _posX, int _posY, int _posZ, int _speed, int _NumberInstruction)
        {
            int newPosX = _posX;
            int newPosY = _posY;
            int newPosZ = _posZ;
            int newInst = _NumberInstruction;


            byte[] buf = new byte[64];

            buf[0] = 0xca;
            //������ ������ ����������
            buf[1] = (byte)(newInst);
            buf[2] = (byte)(newInst >> 8);
            buf[3] = (byte)(newInst >> 16);
            buf[4] = (byte)(newInst >> 24);

            buf[5] = 0x39; //TODO: ���������� ����


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


            int inewSpd = 2328; //TODO: �������� �� ���������

            if (_speed != 0)
            {
                double dnewSpd = (1800 / (double)_speed) * 1000;
                inewSpd = (int)dnewSpd;
            }

            //�������� ��� �
            buf[43] = (byte)(inewSpd);
            buf[44] = (byte)(inewSpd >> 8);
            buf[45] = (byte)(inewSpd >> 16);

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
        /// <summary>
        /// ����� ������� ���������� ��� ������ 
        /// </summary>
        public static List<GKOD_ready> GKODready = new List<GKOD_ready>();

        /// <summary>
        /// ����� ����� ���������� ��� ������ 
        /// </summary>
        public static List<GKOD_raw> GKODraw = new List<GKOD_raw>();

        /// <summary>
        /// ����� ����� �������, ���������� ��� ������������ �����������
        /// </summary>
        //public static List<matrixYline> Matrix = new List<matrixYline>(); 


        /// <summary>
        /// ������� ������
        /// </summary>
        public static void Clear()
        {
            GKODready.Clear();
            GKODraw.Clear();
        }

        private static List<string> parserGkodeLine(string value)
        {

            List<string> lcmd = new List<string>();
            int inx = 0;
            bool collectCommand = false;

            foreach (char symb in value)
            {
                if (symb > 0x40 && symb < 0x5B)  //������� �� A �� Z
                {
                    if (collectCommand)
                    {
                        inx++;
                        collectCommand = false;
                    }

                    collectCommand = true;
                    lcmd.Add("");
                }

                if (collectCommand) lcmd[inx] += symb.ToString();
            }

            return lcmd;
        }

        /// <summary>
        /// ���������� ������, � ���� ������ � G-�����
        /// </summary>
        /// <param name="value">������ � G-�����</param>
        public static void AddData(string value)
        {

            // 1) ��������� ������
            List<string> lcmd = parserGkodeLine(value);

            // 2) �������������� ������ ������
            //    //� ���-�� �������� ������� �� �� ������� ����� � �� �����
            string sGoodsCmd = "";
            string sBadCmd = "";

            foreach (string ss in lcmd)
            {
                    string sCommd = ss.Substring(0, 1).Trim().ToUpper();
                    string sValue = ss.Substring(1).Trim().ToUpper();

                    bool good = false;

                    if (sCommd == "G") //�������� ��������
                    {
                        if (sValue == "0" || sValue == "1") good = true;
                        if (sValue == "00" || sValue == "01") good = true;
                    }

                    if (sCommd == "M") //���/���� ��������
                    {
                        if (sValue == "3" || sValue == "5") good = true;
                        if (sValue == "03" || sValue == "05") good = true;
                    }

                    if (sCommd == "X" || sCommd == "Y" || sCommd == "Z")
                    {
                        //���������� 3-� ���� 
                        good = true;
                        //TODO: ������ ����� ���� ������������ ������
                    }

                    if (good)
                    {
                        sGoodsCmd += ss + " ";
                    }
                    else
                    {
                        sBadCmd += ss + " ";
                    }
            }

            GKODraw.Add(new GKOD_raw(value, sGoodsCmd, sBadCmd, GKODraw.Count));
        }

        //�������������� ������ � GKOD_ready �� GKOD_raw
        public static void CalculateData()
        {

            GKODready.Clear();

            decimal posx = 0, posy = 0, posz = 0;
            int CNC_speedNow = 100;
            bool spindelOn = false;
            bool workspeed = false;


            foreach (GKOD_raw valueGkodRaw in dataCode.GKODraw)
            {
                if (valueGkodRaw.GoodStr == "") continue;

                List<string> lcmd = parserGkodeLine(valueGkodRaw.GoodStr);

                foreach (string ss in lcmd)
                {
                    string value = ss.Trim().ToUpper();


                    if (value == "G0" || value == "G00")
                    {
                        CNC_speedNow = 500;//todo:
                        workspeed = false;
                    }

                    if (value == "G1" || value == "G01")
                    {
                        CNC_speedNow = 200;//todo:
                        workspeed = true;
                    }

                    if (value.Substring(0, 1) == "X")
                    {
                        string value1 = ss.Substring(1).Trim().Replace('.', ',');
                        if (value1.Trim() != "")
                        {
                            try
                            {
                                //������ ��� ������ ������������� �����
                                posx = decimal.Parse(value1);
                            }
                            catch (Exception)
                            {

                                //throw;
                            }
                        }
                        
                    }

                    if (value.Substring(0, 1) == "Y")
                    {
                        string value1 = ss.Substring(1).Trim().Replace('.', ',');
                        if (value1.Trim() != "")
                        {
                            try
                            {
                                //������ ��� ������ ������������� �����
                                posy = decimal.Parse(value1);
                            }
                            catch (Exception)
                            {
                                
                                //throw;
                            }
                            
                        }
                    }

                    if (value.Substring(0, 1) == "Z")
                    {
                        string value1 = ss.Substring(1).Trim().Replace('.', ',');
                        if (value1.Trim() != "")
                        {
                            try
                            {
                                //������ ��� ������ ������������� �����
                                posz = decimal.Parse(value1);
                            }
                            catch (Exception)
                            {

                                //throw;
                            }
                        }
                    }

                    if (value == "M3" || value == "M03") spindelOn = true;

                    if (value == "M5" || value == "M05") spindelOn = false;

                }

                GKODready.Add(new GKOD_ready(valueGkodRaw.numberLine, spindelOn, posx, posy, posz, CNC_speedNow, workspeed));
            }
            

        }
    
    
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

    /// <summary>
    /// ������� ������ �� G-���� ��� ������
    /// </summary>
    public class GKOD_ready
    {
        public decimal X;       // ���������� � ��
        public decimal Y;       // ���������� � ��
        public decimal Z;       // ���������� � ��
        public int speed;       // ��������
        public bool spindelON;  // ���. ��������
        public int numberInstruct; //����� ����������
        public bool workspeed = false;

        public GKOD_ready(int _numberInstruct, bool _spindelON, decimal _X, decimal _Y, decimal _Z, int _speed, bool _workspeed)
        {
            X = _X;
            Y = _Y;
            Z = _Z;
            spindelON = _spindelON;
            numberInstruct = _numberInstruct;
            speed = _speed;
            workspeed = _workspeed;
        }
    }

    /// <summary>
    /// ����� ������ G-���� ��� ������
    /// </summary>
    public class GKOD_raw
    {
        public string FullStr = "";
        public string GoodStr = ""; //��� ������������
        public string BadStr = ""; //��� ��������������
        public int numberLine = 0;

        public GKOD_raw(string _FullStr, string _GoodStr, string _BadStr, int _numberLine)
        {
            FullStr = _FullStr;
            GoodStr = _GoodStr;
            BadStr = _BadStr;
            numberLine = _numberLine;
        }
    }


    #endregion






    public class decPoint
    {

        public decimal X;       // ���������� � ��
        public decimal Y;       // ���������� � ��
        public decimal Z;       // ���������� � ��

        public decPoint(decimal _x, decimal _y, decimal _z)
        {
            X = _x;
            Y = _y;
            Z = _z;
        }
    }

    public class dobPoint
    {

        public double X;       // ���������� � ��
        public double Y;       // ���������� � ��
        public double Z;       // ���������� � ��

        public dobPoint(double _x, double _y, double _z)
        {
            X = _x;
            Y = _y;
            Z = _z;
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
        public static decPoint GetZ(decPoint p1, decPoint p2, decPoint p3, decPoint p4, decPoint p5)
        {
            decPoint p12 = Geometry.CalcPX(p1, p2, p5);
            decPoint p34 = Geometry.CalcPX(p3, p4, p5);

            decPoint p1234 = Geometry.CalcPY(p12, p34, p5);

            return p1234;
        }

        //���������� ������ Z ����� p0, ������� �� ������ ������� ���������� ��� X
        public static decPoint CalcPX(decPoint p1, decPoint p2, decPoint p0)
        {
            decPoint ReturnPoint = new decPoint(p0.X,p0.Y,p0.Z);

            ReturnPoint.Z = p1.Z + (((p1.Z - p2.Z) / (p1.X - p2.X)) * (p0.X - p1.X));

            //TODO: ������ �� ������� ��� ����� 1 � 2 ����� ������ �� �� ����� ���������� ����� ��� �
            ReturnPoint.Y = p1.Y;

            return ReturnPoint;
        }

        //TODO: ������� �� ����
        //���������� ������ Z ����� p0, ������� �� ������ ����� ������� p3 p4  (������ ���������� ��� Y)
        public static decPoint CalcPY(decPoint p1, decPoint p2, decPoint p0)
        {
            decPoint ReturnPoint = new decPoint(p0.X, p0.Y, p0.Z);

            ReturnPoint.Z = p1.Z + (((p1.Z - p2.Z) / (p1.Y - p2.Y)) * (p0.Y - p1.Y));

            return ReturnPoint;
        }

    }
}






///// <summary>
///// ����� ��� ������ � G-�����
///// </summary>
//public static class GKode
//{
//    public static List<LineCommands> kode = new List<LineCommands>();

//    public static int CountRow = 0;

//    /// <summary>
//    /// ��������� ���������� ������������� ��������� ������
//    /// </summary>
//    private static string _stringError = "";
//    /// <summary>
//    /// ��������� ���������� ������������� ��������� ������
//    /// </summary>
//    // ReSharper disable once InconsistentNaming
//    public static string stringError
//    {
//        get { return _stringError; }
//    }

//    /// <summary>
//    /// ������� �� ���� ������
//    /// </summary>
//    public static void Clear()
//    {
//        kode.Clear();
//        CountRow = 0;
//    }


//    //{
//    //    //

//    //}
//    //byte[] readBuffer = new byte[64];
//    //byte[] writeBuffer = new byte[64];
//    //int bytesRead;
//    //int bytesWritten;

//    //while (IsConnect)
//    //{





//    //    //� ��� �� � �������� �������...
//    //    if (statusWorks == EStatusTheads.TaskStart)//_isWorking && !task_RUN
//    //    {
//    //        //TODO: ��� ������ �������, ������� � ������ �������� ���������

//    //        readBuffer = BinaryData.pack_9E(0x05);
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);

//    //        readBuffer = BinaryData.pack_BF(CNC_speedNow, CNC_speedNow, CNC_speedNow);
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);

//    //        readBuffer = BinaryData.pack_C0();
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);
//    //        //task_RUN = true;
//    //        statusWorks = EStatusTheads.TaskWorking;
//    //    }


//    //    if (statusWorks == EStatusTheads.TaskStop)//!_isWorking && task_RUN
//    //    {
//    //        //TODO: ���������� ������� �����������, ���������� ������� ��������� ��������� � ����������
//    //        readBuffer = BinaryData.pack_FF();
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);


//    //        readBuffer = BinaryData.pack_9D();
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);

//    //        readBuffer = BinaryData.pack_9E(0x02);
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);

//    //        for (int i = 0; i < 7; i++)
//    //        {
//    //            readBuffer = BinaryData.pack_FF();
//    //            ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //            System.Threading.Thread.Sleep(1);
//    //        }

//    //        statusWorks = EStatusTheads.Waiting;

//    //    }

//    //    if (statusWorks == EStatusTheads.TaskWorking)
//    //    {


//    //        lineCommands lcmd = gKode.kode[_numWorkingCommand];

//    //        if (lcmd.sGoodsCmd != "")//������ ���������������� ������� �������
//    //        {

//    //            foreach (string ss in lcmd.cmd)
//    //            {
//    //                if (ss == "G0") CNC_speedNow = CNC_speedG0;

//    //                if (ss == "G1") CNC_speedNow = CNC_speedG1;

//    //                if (ss.Substring(0, 1) == "X")
//    //                {
//    //                    string value = ss.Substring(1).Trim().Replace('.', ',');
//    //                    decimal posx = decimal.Parse(value);
//    //                    CNC_pulseX = (int)(posx * axesX.PulsePerMm);
//    //                }

//    //                if (ss.Substring(0, 1) == "Y")
//    //                {
//    //                    string value = ss.Substring(1).Trim().Replace('.', ',');
//    //                    decimal posy = decimal.Parse(value);
//    //                    CNC_pulseY = (int)(posy * axesY.PulsePerMm);
//    //                }

//    //                if (ss.Substring(0, 1) == "Z")
//    //                {
//    //                    string value = ss.Substring(1).Trim().Replace('.', ',');
//    //                    decimal posz = decimal.Parse(value);
//    //                    CNC_pulseZ = (int)(posz * axesZ.PulsePerMm);
//    //                }

//    //                if (ss == "M3" || ss == "M03") Spindel_ON();

//    //                if (ss == "M5" || ss == "M05") Spindel_OFF();

//    //            }
//    //        }

//    //        _numWorkingCommand++;

//    //        if (_numWorkingCommand == gKode.kode.Count) statusWorks = EStatusTheads.TaskStop;

//    //        //_numWorkingCommand
//    //        //_cmd

//    //        //todo 4 pack
//    //        readBuffer = BinaryData.pack_CA(CNC_pulseX, CNC_pulseY, CNC_pulseZ, CNC_speedNow);
//    //        ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //        System.Threading.Thread.Sleep(1);
//    //    }

//    //    // ���� ���� ������� ������� ������ 
//    //    if (arrSend)
//    //    {
//    //        if (arrIndex < ByteArrayToSend.Count)
//    //        {
//    //            readBuffer = ByteArrayToSend[arrIndex];
//    //            ec = usb_writer.Write(readBuffer, 2000, out bytesWritten);
//    //            System.Threading.Thread.Sleep(1);

//    //            arrIndex++;
//    //        }
//    //        else
//    //        {
//    //            arrSend = false;
//    //        }


//    //    }

//    //}




//}





///// <summary>
///// ����� ��� �������� 3D ����������
///// </summary>
//public static class G3D
//{
//    public static List<G3Dpoint> points = new List<G3Dpoint>();




//}


///// <summary>
///// ����� ��� �������� �����
///// </summary>
//public class G3Dpoint
//{
//    public decimal X;
//    public decimal Y;
//    public decimal Z;
//    public bool workspeed;
//    /// <summary>
//    /// ����� ��� ������������� � ���������� �������
//    /// </summary>
//    // ReSharper disable once NotAccessedField.Global
//    private int NumPosition = 0;


//    public G3Dpoint(decimal x, decimal y, decimal z, bool workspeed, int numPosition)
//    {
//        X = x;
//        Y = y;
//        Z = z;
//        this.workspeed = workspeed;
//        NumPosition = numPosition;
//    }
//}



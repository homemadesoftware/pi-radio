using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace pi_radio
{
    public class RadioHardwareMonitor
    {
        private const string device = "/dev/i2c-1";
        private const int slaveAddress = 0x8;
        private bool isOn;
        private int channelSelection;
        private int volume;


        public RadioHardwareMonitor()
        {

        }

        public bool Refresh()
        {
            const int bufferLength = 7;
            byte[] buffer = new byte[bufferLength];

            int status = ReadI2C(device, slaveAddress, buffer, bufferLength);

            if (status > 0)
            {
                byte size = buffer[0];
                byte onOff = buffer[1];
                byte channelValue = buffer[2];
                byte volumeLow = buffer[3];
                byte volumeHigh = buffer[4];
                byte cookieLow = buffer[5];
                byte cookieHigh = buffer[6];

                if (cookieHigh == 'H' && cookieLow == 'C')
                {
                    isOn = onOff != 0;
                    volume = (((int)volumeHigh << 8)) | volumeLow;
                    channelSelection = channelValue;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        public bool IsOn => isOn;

        public int ChannelSelection => channelSelection;

        public int Volume => volume;

        [DllImport("/home/pi/pi-i2device/pi-i2cdevice.so")]
        static extern int ReadI2C(string device, int slaveAddress, byte[] buffer, int bufferLength);


    }
}

using System;
using System.Diagnostics;
using Windows.Devices.I2c;

namespace VipX1.Pi3.Sensors
{
    /// <summary>
    /// Uses Windows.Devices.I2c to communicate with the Sparkfun Si7021
    /// humidity and temperature sensor breakout board and collect it's values
    /// Note: Use GpioController.GetDefault to check for GPIO's before calling Si7021.Setup
    /// </summary>
    public class Si7021
    {
        /* Used I2CDetect on Raspian to find the address */
        private Byte address = 0x40;
        /* The option of either clock stretching (Hold Master Mode) 
         * or Not Acknowledging read requests (No Hold Master Mode) 
         * is available to indicate to the master that the measurement is in progress */
        private Byte temp_measure_hold = 0xE3; // Hold Master
        private Byte humid_measure_hold = 0xE5;
        private Byte temp_measure_nohold = 0xF3; // Don't hold Master
        private Byte humid_measure_nohold = 0xF5;
        /* 0xE0 does not perform a measurement but returns the temperature value 
         * measured during the relative humidity measurement and is more efficient */
        private Byte previous_temperature = 0xE0;
        private I2cDevice si7021;

        /// <summary>
        /// Gets an configures the I2c device (Si7021) and makes an initial measurement
        /// </summary>
        public async void Setup()
        {
            I2cConnectionSettings settings = new I2cConnectionSettings(address);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            var controller = await I2cController.GetDefaultAsync();
            si7021 = controller.GetDevice(settings);

            try
            {
                /* Get the current temperature and humidity values and check everything works */
                si7021.Write(new byte[] { humid_measure_hold, 0x00 });
            }
            catch (Exception ex)
            {
                ex.Source = "Weather.Setup (VipX1.Pi3.Sensors)";
                Debug.Write(ex);
                throw;
            }
        }

        public double[] GetValues()
        {
            double humidity;
            double temperature;

            try
            {
                byte[] writeBuf_MeasureHumity = new byte[] { humid_measure_hold, 0x00 };
                byte[] writeBuf_PreviousTemperature = new byte[] { previous_temperature, 0x00 };

                byte[] readBuf_MeasureHumity = new byte[2];
                byte[] readBuf_PreviousTemperature = new byte[2];

                si7021.WriteRead(writeBuf_MeasureHumity, readBuf_MeasureHumity);
                si7021.WriteRead(writeBuf_PreviousTemperature, readBuf_PreviousTemperature);

                /* Algorithm taken from: SparkFun_Si7021_Breakout_Library.cpp 
                   See ReadMe.md for links */

                /* Clear the last to bits of LSB to 00.
                   According to data sheet LSB of RH is always xxxxxx10 */

                byte lsb = 0xFC;
                humidity = (125.0 * (readBuf_MeasureHumity[0] << 8 | lsb) / 65536) - 6;
                temperature = (175.72 * (readBuf_PreviousTemperature[0] << 8 | lsb) / 65536) - 46.85;

                Debug.Write(string.Format("Humidity:\t{0} %\r\nTemperature:\t{1} C\r\n", humidity, temperature));
            }
            catch (Exception ex)
            {
                ex.Source = "Weather.GetValues (VipX1.Pi3.Sensors)";
                Debug.WriteLine(ex.Message);
                throw;
            }

            return new double[] { humidity, temperature };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace pi_radio
{
    public class Program
    {
        private static SortedDictionary<int, string> GetChannels()
        {
            SortedDictionary<int, string> table = new SortedDictionary<int, string>();
            table.Add(16, "http://stream.live.vc.bbcmedia.co.uk/bbc_radio_one");
            table.Add(19, "http://stream.live.vc.bbcmedia.co.uk/bbc_radio_one");
            table.Add(25, "http://stream.live.vc.bbcmedia.co.uk/bbc_radio_one");
            table.Add(31, "http://stream.live.vc.bbcmedia.co.uk/bbc_radio_one");
            table.Add(41, "http://stream.live.vc.bbcmedia.co.uk/bbc_radio_one");
            table.Add(49, "http://stream.live.vc.bbcmedia.co.uk/bbc_radio_one");
            return table;
        }

        public static void Main(string[] args)
        {
            bool? lastOn = null;
            int lastVolume = -1;
            int lastChannel = -1;

            RadioHardwareMonitor monitor = new RadioHardwareMonitor();

            while (true)
            {
                if (monitor.Refresh())
                {
                    bool onOff = monitor.IsOn;
                    int volume = monitor.Volume;
                    int channel = monitor.ChannelSelection;

                    if ((channel != lastChannel) || (!lastOn.HasValue || onOff != lastOn.Value) || (lastVolume - volume) > 2 || (volume - lastVolume) > 2)
                    {
                        MusicPlayerDaemonClient client = new MusicPlayerDaemonClient();

                        if (channel != lastChannel)
                        {
                            var channels = GetChannels();
                            channels.ToList().ForEach(de => client.SetChannel(de.Key, de.Value));
                            client.Play(channel);
                            Console.WriteLine($"Playing Channel: {channel}");
                            lastChannel = channel;
                        }

                        if (!lastOn.HasValue || onOff != lastOn.Value)
                        {
                            Console.WriteLine($"n: {onOff}");
                            if (onOff)
                            {
                                client.Play(lastChannel);
                            }
                            else
                            {
                                client.Play(0);
                            }

                            lastOn = onOff;
                        }

                        Console.WriteLine($"Volume: {volume}");

                        int adjustedVolume = (50 * Math.Pow(volume, 0.5) / 32) + 50;
                        client.SetVolume(adjustedVolume);
                        lastVolume = volume;
                    }

                }
            }
        }
    }
}

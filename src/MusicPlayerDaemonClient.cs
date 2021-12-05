using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace pi_radio
{
    public class MusicPlayerDaemonClient
    {
		private const int port = 6600;
		private Socket socket;
		private const int timeout = 15000;

		public void SetOnOff(bool isOn)
        {
            RunCommand("pause", isOn.ToString());
        }

		public void SetVolume(int level)
		{
			RunCommand("setvol", level.ToString());
		}

		public void SetChannel(int channel, string url)
		{
			string playlistName = "chx" + channel.ToString();
			RunCommand("rm", playlistName);
			RunCommand("playlistadd", playlistName + " " + url);
		}


		public void Play(int channel)
		{
			string playlistName = "chx" + channel.ToString();
			RunCommand("clear", "");
			RunCommand("load", playlistName);
			RunCommand("play", "-1");
		}


		private string RunCommand(string command, string argument)
        {
			StringBuilder builder = new StringBuilder();
			builder.AppendLine("command_list_begin");
			builder.Append(command);
			if (!string.IsNullOrEmpty(argument))
			{
				builder.Append($" {argument}");
			}
			builder.AppendLine();
			builder.AppendLine("command_list_end");
			return SendAndReceive(builder.ToString());
		}

		private string SendAndReceive(string data)
		{
			using (MemoryStream commandStream = new MemoryStream())
			{
				StreamWriter writer = new StreamWriter(commandStream);
				writer.Write(data);
				writer.Flush();

				if (socket == null)
                {
					socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				}

				IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, port);

				try
				{
					CompletionState state;
					if (!socket.Connected)
					{
						state = new CompletionState(socket);
						socket.BeginConnect(endPoint, new AsyncCallback(OnConnectEnd), state);
						if (!state.HasCompleted.WaitOne(timeout) || state.CompletionStatus == 0)
						{
							throw new ApplicationException("Failed to connect");
						}
					}

					byte[] buffer = commandStream.ToArray();
					state = new CompletionState(socket);
					socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(OnSendEnd), state);
					if (!state.HasCompleted.WaitOne(timeout) || state.CompletionStatus < 0)
					{
						throw new ApplicationException("Failed to send");
					}

					byte[] recvBuffer = new byte[1024];
					state = new CompletionState(socket);
					socket.BeginReceive(recvBuffer, 0, recvBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceiveEnd), state);
					if (!state.HasCompleted.WaitOne(timeout) || state.CompletionStatus < 0)
					{
						throw new ApplicationException("Failed to recv");
					}

					using (MemoryStream response = new MemoryStream(recvBuffer))
                    {
						using (StreamReader reader = new StreamReader(response))
                        {
							return reader.ReadToEnd();
                        }
                    }
				}
				catch (Exception e)
                {
					Console.WriteLine(e);
					socket.Shutdown(SocketShutdown.Both);
					socket.Close();
					socket = null;
					return null;
				}
			}
		}

		private void OnConnectEnd(IAsyncResult asyncResult)
        {
			var state = (CompletionState)asyncResult.AsyncState;
			state.Socket.EndConnect(asyncResult);
			state.CompletionStatus = state.Socket.Connected ? 1 : 0;
			state.HasCompleted.Set();

		}

		private void OnSendEnd(IAsyncResult asyncResult)
        {
			var state = (CompletionState)asyncResult.AsyncState;
			state.CompletionStatus = state.Socket.EndSend(asyncResult);
			state.HasCompleted.Set();
		}

		private void OnReceiveEnd(IAsyncResult asyncResult)
		{
			var state = (CompletionState)asyncResult.AsyncState;
			state.CompletionStatus = state.Socket.EndReceive(asyncResult);
			state.HasCompleted.Set();
		}

		private class CompletionState
        {
			public CompletionState(Socket socket)
            {
				Socket = socket;
				HasCompleted = new ManualResetEvent(false);
            }

			public Socket Socket { get; init; }

			public ManualResetEvent HasCompleted { get; init; }

			public int CompletionStatus { get; set; }
        }

	}
}

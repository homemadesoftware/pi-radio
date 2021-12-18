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

		public bool SetOnOff(bool isOn)
        {
			return RunCommands(new[] 
			{ 
				new Tuple<string, string>("pause", isOn.ToString()) }
			);
        }

		public bool SetVolume(int level)
		{
			return RunCommands(new[]
			{
				new Tuple<string, string>("setvol", level.ToString()) }
			);
		}

		public bool SetChannels(IDictionary<int, string> channels)
		{
			List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
			foreach (var channel in channels)
            {
				string playlistName = "chx" + channel.Key.ToString();
				commandList.Add(new Tuple<string, string>("rm", playlistName));
				commandList.Add(new Tuple<string, string>("playlistadd", playlistName + " " + channel.Value));
			}
			return RunCommands(commandList);
		}


		public bool Play(int channel)
		{
			string playlistName = "chx" + channel.ToString();
			return RunCommands(new[]
			{
				new Tuple<string, string>("clear", ""),
				new Tuple<string, string>("load", playlistName),
				new Tuple<string, string>("play", "-1")
			});
		}


		private bool RunCommands(IList<Tuple<string, string>> commandEntries)
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendLine("command_list_begin");
			foreach (var commandEntry in commandEntries)
			{
				builder.Append(commandEntry.Item1);
				if (!string.IsNullOrEmpty(commandEntry.Item2))
				{
					builder.Append($" {commandEntry.Item2}");
				}
				builder.AppendLine();
			}
			builder.AppendLine("command_list_end");
			string response = SendAndReceive(builder.ToString());
			return response.Contains("OK");
		}

		private string SendAndReceive(string data)
		{
			Console.WriteLine(data);
			using (MemoryStream commandStream = new MemoryStream())
			{
				StreamWriter writer = new StreamWriter(commandStream);
				writer.Write(data);
				writer.Flush();

				if (socket == null)
                {
					socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				}

				IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

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

					using (MemoryStream responseStream = new MemoryStream(recvBuffer))
                    {
						using (StreamReader reader = new StreamReader(responseStream))
                        {
							string response = reader.ReadToEnd();
							Console.WriteLine(response);
							return response;
                        }
                    }
				}
				catch (Exception e)
                {
					Console.WriteLine(e);
					try
					{
						socket.Shutdown(SocketShutdown.Both);
					}
					catch 
					{

					}
					socket.Close();
					socket = null;
					return null;
				}
			}
		}

		private void OnConnectEnd(IAsyncResult asyncResult)
        {
			CompletionState state = (CompletionState)asyncResult.AsyncState;
			try
            {
				state.Socket.EndConnect(asyncResult);
				state.CompletionStatus = state.Socket.Connected ? 1 : 0;
				state.HasCompleted.Set();
			}
			catch (Exception e)
            {
				Console.WriteLine(e);
				state.CompletionStatus = 0;
				state.HasCompleted.Set();
			}
			

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

﻿using System;
using System.Net;
using System.Net.Sockets;

namespace System.Net.FtpClient {
	public class FtpActiveStream : FtpDataStream {
		public override bool Execute(string command) {
			// if we're already connected we need
			// to reset ourselves and start over
			if(this.Socket.Connected) {
				this.Close();
			}

			if(!this.Socket.Connected) {
				this.Open();
			}

			try {
				this.CommandChannel.LockCommandChannel();
				this.CommandChannel.Execute(command);

				if(this.CommandChannel.ResponseStatus && !this.Socket.Connected) {
					this.Accept();
				}

				return this.CommandChannel.ResponseStatus;
			}
			finally {
				this.CommandChannel.UnlockCommandChannel();
			}
		}

		protected void Accept() {
			this.Socket = this.Socket.Accept();
		}

		protected override void Open(FtpDataChannelType type) {
			string ipaddress = null;
			int port = 0;

			this.Socket.Bind(new IPEndPoint(((IPEndPoint)this.CommandChannel.LocalEndPoint).Address, 0));
			this.Socket.Listen(1);

			ipaddress = ((IPEndPoint)this.Socket.LocalEndPoint).Address.ToString();
			port = ((IPEndPoint)this.Socket.LocalEndPoint).Port;

			try {
				this.CommandChannel.LockCommandChannel();

				switch(type) {
					case FtpDataChannelType.ExtendedActive:
						this.CommandChannel.Execute("EPRT |1|{0}|{1}|", ipaddress, port);
						if(this.CommandChannel.ResponseType == FtpResponseType.PermanentNegativeCompletion) {
							this.CommandChannel.RemoveCapability(FtpCapability.EPSV);
							this.CommandChannel.RemoveCapability(FtpCapability.EPRT);
							this.CommandChannel.Execute("PORT {0},{1},{2}",
								ipaddress.Replace(".", ","), port / 256, port % 256);
							type = FtpDataChannelType.Active;
						}
						break;
					case FtpDataChannelType.Active:
						this.CommandChannel.Execute("PORT {0},{1},{2}",
							ipaddress.Replace(".", ","), port / 256, port % 256);
						break;
					default:
						throw new Exception("Active streams do not support " + type.ToString());
				}

				if(!this.CommandChannel.ResponseStatus) {
					throw new FtpException(this.CommandChannel.ResponseMessage);
				}
			}
			finally {
				this.CommandChannel.UnlockCommandChannel();
			}
		}

		public FtpActiveStream(FtpCommandChannel chan)
			: base() {
			if(chan == null) {
				throw new ArgumentNullException("chan");
			}
			this.CommandChannel = chan;
		}
	}
}

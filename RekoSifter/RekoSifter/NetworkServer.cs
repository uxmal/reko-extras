using Reko.Core.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RekoSifter
{
    enum PacketType : byte
    {
        MakeSifter = 1,
        DoElfObject,
        StopSifter,
        /*
        StartSession,
        EndSession
        */
    }

    class NetworkTextWriter : TextWriter
    {
        private readonly BinaryWriter writer;

        public NetworkTextWriter(TcpClient client) : base()
        {
            this.writer = new BinaryWriter(client.GetStream());
        }

        public override Encoding Encoding => Encoding.ASCII;

        public override void Write(char ch)
        {
            throw new InvalidOperationException();
        }

        public override void WriteLine(string? value)
        {
            var buf = new byte[4 + value.Length];
            BinaryPrimitives.WriteInt32BigEndian(buf, value.Length);
            this.Encoding.GetBytes(value).CopyTo(buf, 4);
            writer.Write(buf);
        }
    }

    public class NetworkServer
    {
        private TcpListener server;
        
        public NetworkServer()
        {
            server = new TcpListener(new IPEndPoint(IPAddress.Any, 1337));
        }

        private void HandleClient(TcpClient client)
        {
            var s = new BinaryReader(client.GetStream());
            var anotherOne = true;

            Sifter? sifter = null;

            var ownDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var os = new StreamWriter(new FileStream(
                Path.Combine(ownDir, "server.log"),
                FileMode.OpenOrCreate | FileMode.Append,
                FileAccess.Write,
                FileShare.Read));

            Func<string> readString = () =>
            {
                var l = BinaryPrimitives.ReadInt32BigEndian(s.ReadBytes(4));
                return Encoding.ASCII.GetString(s.ReadBytes(l));
            };

            while (anotherOne)
            {
                var type = s.ReadByte();
                if (!Enum.IsDefined(typeof(PacketType), type))
                {
                    throw new InvalidDataException();
                }

                var sizeField = BinaryPrimitives.ReadInt32BigEndian(s.ReadBytes(4));
                var eType = (PacketType) type;
                switch (eType)
                {
                case PacketType.MakeSifter:
                    var args = Enumerable.Range(0, sizeField)
                        .Select((_) =>
                        {
                            return readString();
                        }).ToArray();
                    sifter = new Sifter(args);
                    //sifter.SetOutputStream(null);
                    //sifter.SetOutputStream(new NetworkTextWriter(client));
                    sifter.SetOutputStream(os);
                    break;
                case PacketType.DoElfObject:
                    var name = readString();
                    sifter.OutputLine($"Incoming object for '{name}'");

                    sifter.OutputLine($"Object size: {sizeField} B");
                    //sifter.OutputLine($"Incoming object for '{name}'");
                    var bytes = s.ReadBytes(sizeField);
                    try
                    {
                        sifter.DasmElfObject(bytes);
                    } catch(Exception ex)
                    {
                        sifter.ErrorLine("FATAL: " + ex.ToString());
                    }

                    var search = "/gas/testsuite";
                    var abstractName = name
                        .Substring(name.IndexOf(search) + search.Length)
                        .Replace('/', '_');
                    sifter.RenameTestFiles(abstractName);
                    break;
                case PacketType.StopSifter:
                    anotherOne = false;
                    os.Close();
                    break;
                }
            }
        }

        private void MainLoop()
        {
            bool anotherOne = true;
            while (anotherOne)
            {
                var client = server.AcceptTcpClient();
                try
                {
                    HandleClient(client);
                } finally
                {
                    client.Close();
                }
            }
        }

        public void StartMainLoop()
        {
            server.Start();
            try
            {
                MainLoop();
            } finally
            {
                server?.Stop();
            }
        }
    }
}

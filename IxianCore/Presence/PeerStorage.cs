﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DLT.Meta;

namespace DLT
{
    class PeerStorage
    {
        class Peer
        {
            public string hostname;
            public byte[] walletAddress;
            public DateTime lastSeen;
            public long lastConnectAttempt;

            public Peer(string iHostname, byte[] iWalletAddress, DateTime iLastSeen, long iLastConnectAttempt)
            {
                hostname = iHostname;
                walletAddress = iWalletAddress;
                lastSeen = iLastSeen;
                lastConnectAttempt = iLastConnectAttempt;
            }
        };

        private static List<Peer> peerList = new List<Peer>();

        public static string peersFilename = "peers.dat";

        public static bool addPeerToPeerList(string hostname, byte[] walletAddress, bool storePeersFile = true)
        {
            if(!validateHostname(hostname))
            {
                return false;
            }
            Peer p = new Peer(hostname, walletAddress, DateTime.UtcNow, 0);

            lock (peerList)
            {
                if (peerList.Exists(x => x.hostname == hostname))
                {
                    p.lastConnectAttempt = peerList.Find(x => x.hostname == hostname).lastConnectAttempt;
                }

                if(peerList.RemoveAll(x => x.hostname == hostname) > 0)
                {
                    storePeersFile = false; // this hostname:port is already in the file, no need to add it again

                }

                if (walletAddress != null)
                {
                    peerList.RemoveAll(x => x.walletAddress != null && x.walletAddress.SequenceEqual(walletAddress));
                }

                peerList.Add(p);

                if (peerList.Count > 500)
                {
                    DateTime minLastSeen = peerList.Min(x => x.lastSeen);
                    peerList.RemoveAll(x => x.lastSeen == minLastSeen);
                }
            }

            if (storePeersFile)
            {
                savePeersFile();
                return true;
            }
            return false;
        }

        public static bool removePeer(string hostname)
        {
            if (peerList.RemoveAll(x => x.hostname == hostname) > 0)
            {
                savePeersFile();
                return true;
            }
            return false;
        }

        public static bool validateHostname(string address)
        {
            // Check if the address format is correct
            string[] server = address.Split(':');
            if (server.Count() < 2)
            {
                return false;
            }

            if(server[0] == "127.0.0.1"
                || server[0] == "::1"
                || server[0] == "localhost")
            {
                return false;
            }

            return true;
        }

        public static string getRandomMasterNodeAddress()
        {
            List<Peer> connectableList = null;
            lock (peerList)
            {
                long curTime = Clock.getTimestamp();
                connectableList = peerList.FindAll(x => curTime - x.lastConnectAttempt > 30);
                if (connectableList != null && connectableList.Count > 0)
                {
                    Random rnd = new Random();
                    Peer p = connectableList[rnd.Next(connectableList.Count)];
                    p.lastConnectAttempt = curTime;
                    return p.hostname;
                }
            }
            return "";
        }

        // Saves a list of 500 master node addresses to a file
        public static void savePeersFile()
        {
            lock (peerList)
            {
                // Don't write to file if no masternode presences were found in addition to the current node
                if (peerList.Count < 2)
                    return;

                using (TextWriter tw = new StreamWriter(peersFilename))
                {
                    foreach (Peer p in peerList)
                    {
                        if (p.walletAddress != null)
                        {
                            tw.WriteLine(p.hostname + ";" + Base58Check.Base58CheckEncoding.EncodePlain(p.walletAddress));
                        }else
                        {
                            tw.WriteLine(p.hostname);
                        }
                    }
                    tw.Flush();
                    tw.Close();
                }
            }
        }

        // Retrieves the master node address file's contents
        public static bool readPeersFile()
        {
            // Check if the presence file exists
            if (File.Exists(peersFilename))
            {
                Logging.info("Peers file found. Adding addresses to initial connections.");
            }
            else
            {
                return false;
            }

            try
            {
                lock (peerList)
                {
                    peerList.Clear();
                    List<string> ips = File.ReadAllLines(peersFilename).ToList();
                    foreach (string ip in ips)
                    {
                        string[] split_hostname = ip.Split(';');
                        if (split_hostname.Length == 2)
                        {
                            addPeerToPeerList(split_hostname[0], Base58Check.Base58CheckEncoding.DecodePlain(split_hostname[1]), false);
                        }
                        else
                        {
                            addPeerToPeerList(ip, null, false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        // Deletes the presence file cache
        public static void deletePeersFile()
        {
            if (File.Exists(peersFilename))
            {
                File.Delete(peersFilename);
            }

            return;
        }

    }
}

using Microsoft.AspNetCore.Hosting.Server;
using System;
using System.Collections.Concurrent;
using System.Text;

namespace TestAPILayer
{
    public sealed class KeyStore
    {
        private class Keys
        {
            public List<byte[]> ENCRYPTS { get; set; } = new List<byte[]>();
            public List<byte[]> SIGNS { get; set; } = new List<byte[]>();

            public List<byte[]> SE_PRIV { get; set; } = new List<byte[]>();

            public byte[] DS_PUB;
            public byte[] DE_PUB;
            public byte[] NONCE;
            public byte[] wTOKEN;
        }

        private ConcurrentDictionary<string, Keys> _KEYS = new ConcurrentDictionary<string, Keys>();

        // This is required to make the Servers singleton class thread safe
        private static readonly Lazy<KeyStore> _lazy = new Lazy<KeyStore>(() => new KeyStore());

        // Static instance of the Servers class
        public static KeyStore Inst { get { return _lazy.Value; } }

        private string ByteArrayToString(byte[] bytes)
        {
            var sb = new StringBuilder();
            sb.Append(bytes);
            return sb.ToString();
        }

        public void StoreENCRYPTS(byte[] SRC, List<string> ENCRYPTS)
        {
            string srcID = ByteArrayToString(SRC);

            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].ENCRYPTS.Clear();
            for (int i = 0; i < ENCRYPTS.Count; i++)
            {
                _KEYS[srcID].ENCRYPTS.Add(CryptoUtils.CBORBinaryStringToBytes(ENCRYPTS[i]));
            }
        }

        public void StoreENCRYPTS(byte[] SRC, List<byte[]> ENCRYPTS)
        {
            string srcID = ByteArrayToString(SRC);

            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].ENCRYPTS.Clear();
            for (int i = 0; i < ENCRYPTS.Count; i++)
            {
                _KEYS[srcID].ENCRYPTS.Add(ENCRYPTS[i]);
            }
        }

        public void StoreSIGNS(byte[] SRC, List<string> SIGNS)
        {
            string srcID = ByteArrayToString(SRC);

            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].SIGNS.Clear();
            for (int i = 0; i < SIGNS.Count; i++)
            {
                _KEYS[srcID].SIGNS.Add(CryptoUtils.CBORBinaryStringToBytes(SIGNS[i]));
            }
        }

        public void StoreSIGNS(byte[] SRC, List<byte[]> SIGNS)
        {
            string srcID = ByteArrayToString(SRC);

            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].SIGNS.Clear();
            for (int i = 0; i < SIGNS.Count; i++)
            {
                _KEYS[srcID].SIGNS.Add(SIGNS[i]);
            }
        }

        public void StoreSE_PRIV(byte[] SRC, List<string> SE_PRIV)
        {
            string srcID = ByteArrayToString(SRC);

            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].SE_PRIV.Clear();
            for (int i = 0; i < SE_PRIV.Count; i++)
            {
                _KEYS[srcID].SE_PRIV.Add(CryptoUtils.CBORBinaryStringToBytes(SE_PRIV[i]));
            }
        }

        public void StoreSE_PRIV(byte[] SRC, List<byte[]> SE_PRIV)
        {
            string srcID = ByteArrayToString(SRC);

            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].SE_PRIV.Clear();
            for (int i = 0; i < SE_PRIV.Count; i++)
            {
                _KEYS[srcID].SE_PRIV.Add(SE_PRIV[i]);
            }
        }

        public void StoreDS_PUB(byte[] SRC, byte[] DS_PUB)
        {
            string srcID = ByteArrayToString(SRC);
            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].DS_PUB = new byte[DS_PUB.Length];
            Array.Copy(DS_PUB, _KEYS[srcID].DS_PUB, DS_PUB.Length);
        }

        public void StoreDE_PUB(byte[] SRC, byte[] DE_PUB)
        {
            string srcID = ByteArrayToString(SRC);
            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].DE_PUB = new byte[DE_PUB.Length];
            Array.Copy(DE_PUB, _KEYS[srcID].DE_PUB, DE_PUB.Length);
        }

        public void StoreNONCE(byte[] SRC, byte[] NONCE)
        {
            string srcID = ByteArrayToString(SRC);
            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].NONCE = new byte[NONCE.Length];
            Array.Copy(NONCE, _KEYS[srcID].NONCE, NONCE.Length);
        }

        public void StoreWTOKEN(byte[] SRC, byte[] wTOKEN)
        {
            string srcID = ByteArrayToString(SRC);
            if (!_KEYS.ContainsKey(srcID))
            {
                _KEYS.TryAdd(srcID, new Keys());
            }
            _KEYS[srcID].wTOKEN = new byte[wTOKEN.Length];
            Array.Copy(wTOKEN, _KEYS[srcID].wTOKEN, wTOKEN.Length);
        }


        public List<byte[]> GetENCRYPTS(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].ENCRYPTS;
        }

        public List<byte[]> GetSIGNS(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].SIGNS;
        }

        public List<byte[]> GetSE_PRIV(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].SE_PRIV;
        }

        public byte[] GetNONCE(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].NONCE;
        }

        public byte[] GetWTOKEN(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].wTOKEN;
        }

        public byte[] GetDS_PUB(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].DS_PUB;
        }

        public byte[] GetDE_PUB(byte[] SRC)
        {
            string srcID = ByteArrayToString(SRC);
            return _KEYS[srcID].DE_PUB;
        }
    }
}

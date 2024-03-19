﻿namespace TestAPILayer.Requests
{
    public sealed class LoginRequest : BaseRequest
    {
        public string encKEY { get; set; }

        public string DS_PUB;
        public string DE_PUB;
        public string NONCE;

        public List<string> WENCRYPTS { get; set; }
        public List<string> WSIGNS { get; set; }
    }
}
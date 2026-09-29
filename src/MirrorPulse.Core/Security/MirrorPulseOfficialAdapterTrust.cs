using System.Security.Cryptography;

namespace MirrorPulse.Core.Security;

/// <summary>Built-in trust anchor for official MirrorPulse Adapter packages.</summary>
public static class MirrorPulseOfficialAdapterTrust
{
    public const string Signer = "MirrorPulse Team";

    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAqX6euBD2CAhDZ0Zp+4wj
xZg7NKQvhAWnLwhGx+MKqxPDbSNA91I1iCOm687Ddc2zqtSxOk2OTuU7XNJayUwh
qjRPNgl9L0B/Kbz2sttG9YH1jPxvQykAFWr1cz7sp440wKNl54t5uHhbkSy6iOqr
/uxDdJ0wYXACq7aIPT6cPJL4liZQ+cofRixEKggaiTd9Z/F8PT44GINCHcf48Pby
Zc5kHSLb0uaHoIbTakAr3WIeyM0XZX8Ha1qH/QSeYkWBPLnv/hBmW8Qxnm1u0R1s
yvwQK5aTYYl1iaRsi48bBJ/j0Jq3bjhT/gVUbqyaPgK3KaDK1UHLjdjjSWl6IqgY
qdgphE4lD+RXanS8VOOJGd/Cevs73uo30Cn+/RSa8ikkCcP43H0WhB5odKftc8Q1
OPfjbLLJl8dkS1ntP/8rHVQgtm1lPU7svrNURX2OK4oUUayHUAkmjuGtfriPbfwO
/n94r3P1nuCR9Ml0WQXJFnbMhJsxOJzH5fQhW1U4K2UNt6WVfsECusWEtzSicHK1
R47vkvbqJDVO3LC0gWnjDThtPLCaVKd+OxQxb1mHpeHM/xccqDhYYx+QEFzgtkdK
pn1kPtoCXmwFNJxc2NwM5oBVfsJzgqkIjEIBxlDUJyTn54tyPYQA5IxBxmhswZgA
jyAm4eMy+hH+s07x7NnS2HECAwEAAQ==
-----END PUBLIC KEY-----
""";

    public static RSA CreatePublicKey()
    {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(PublicKeyPem);
        return rsa;
    }
}

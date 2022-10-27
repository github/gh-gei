using System;
using System.Reflection;
using System.Security.Cryptography;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;
using Renci.SshNet.Security.Cryptography;
using Renci.SshNet.Security.Cryptography.Ciphers;

namespace OctoshiftCLI.BbsToGithub;

// workaround for RSA keys on Ubuntu 22.04
// https://github.com/sshnet/SSH.NET/issues/825#issuecomment-1139440419

public class RsaWithSha256SignatureKey : RsaKey
{
    public RsaWithSha256SignatureKey(BigInteger modulus, BigInteger exponent, BigInteger d, BigInteger p, BigInteger q,
        BigInteger inverseQ) : base(modulus, exponent, d, p, q, inverseQ)
    {
    }

    private RsaSha256DigitalSignature _digitalSignature;

    protected override DigitalSignature DigitalSignature
    {
        get
        {
            _digitalSignature ??= new RsaSha256DigitalSignature(this);

            return _digitalSignature;
        }
    }

    public override string ToString() => "rsa-sha2-256";
}

public class RsaSha256DigitalSignature : CipherDigitalSignature, IDisposable
{
    private HashAlgorithm _hash;

    public RsaSha256DigitalSignature(RsaWithSha256SignatureKey rsaKey)
        // custom OID
        : base(new ObjectIdentifier(2, 16, 840, 1, 101, 3, 4, 2, 1), new RsaCipher(rsaKey))
    {
        // custom
        _hash = SHA256.Create();
    }

    protected override byte[] Hash(byte[] input) => _hash.ComputeHash(input);

    private bool _isDisposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            var hash = _hash;
            if (hash != null)
            {
                hash.Dispose();
                _hash = null;
            }

            _isDisposed = true;
        }
    }

    ~RsaSha256DigitalSignature()
    {
        Dispose(false);
    }
}

public static class RsaSha256Util
{
    public static void SetupConnection(ConnectionInfo connection)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        connection.HostKeyAlgorithms["rsa-sha2-256"] = data => new KeyHostAlgorithm("rsa-sha2-256", new RsaKey(), data);
    }

    public static RsaWithSha256SignatureKey ConvertToKeyWithSha256Signature(PrivateKeyFile keyFile)
    {
        return keyFile?.HostKey is not KeyHostAlgorithm oldKeyHostAlgorithm
            ? throw new ArgumentException("HostKey must be a KeyHostAlgorithm", nameof(keyFile))
            : oldKeyHostAlgorithm.Key is not RsaKey oldRsaKey
            ? throw new ArgumentException("HostKey.Key must be a RsaKey", nameof(keyFile))
            : new RsaWithSha256SignatureKey(oldRsaKey.Modulus, oldRsaKey.Exponent, oldRsaKey.D, oldRsaKey.P, oldRsaKey.Q, oldRsaKey.InverseQ);
    }

    public static void UpdatePrivateKeyFile(PrivateKeyFile keyFile, RsaWithSha256SignatureKey key)
    {
        var keyHostAlgorithm = new KeyHostAlgorithm(key?.ToString(), key);

        var hostKeyProperty = typeof(PrivateKeyFile).GetProperty(nameof(PrivateKeyFile.HostKey));
        hostKeyProperty.SetValue(keyFile, keyHostAlgorithm);

        var keyField = typeof(PrivateKeyFile).GetField("_key", BindingFlags.NonPublic | BindingFlags.Instance);
        keyField.SetValue(keyFile, key);
    }
}

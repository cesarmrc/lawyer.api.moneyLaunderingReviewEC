namespace SecureHumanLoopCaptcha.Shared.Security;

public interface IEncryptionService
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}

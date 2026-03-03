namespace crm_api.Interfaces
{
    public interface IEncryptionService
    {
        string Encrypt(string plain);
        string Decrypt(string cipher);
    }
}

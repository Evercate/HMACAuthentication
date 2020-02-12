using System.Threading.Tasks;

namespace HMACAuthentication.Authentication
{
    public interface ISecretLookup
    {
        Task<string> LookupAsync(string id);
    }
}

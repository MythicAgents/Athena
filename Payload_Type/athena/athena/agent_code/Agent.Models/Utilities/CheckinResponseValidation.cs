using Agent.Models;

namespace Agent.Utilities
{
    public static class CheckinResponseValidation
    {
        public static bool IsSuccessful(CheckinResponse? response) =>
            response?.action == "checkin" &&
            response.status == "success" &&
            !string.IsNullOrWhiteSpace(response.id);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Valuator.Pages;

public class ClientErrorModel : PageModel
{
    public int ErrorCode { get; set; }
    public string ErrorTitle { get; set; } = "Error";
    public string ErrorMessage { get; set; } = "Something went wrong.";

    public void OnGet(int? statusCode)
    {
        ErrorCode = statusCode ?? 400;
        HttpContext.Response.StatusCode = ErrorCode;

        switch (ErrorCode)
        {
            case 403:
                ErrorTitle = "Access Denied";
                ErrorMessage = "You do not have permission to view this resource.";
                break;
            case 404:
                ErrorTitle = "Page Not Found";
                ErrorMessage = "The requested page could not be found.";
                break;
            default:
                ErrorTitle = "Client Error";
                ErrorMessage = "An error occurred while processing your request.";
                break;
        }
    }
}
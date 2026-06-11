using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
namespace MyPosAccurate2026;

public partial class App : Application
{
    public static string API_HOST { get; set; }
    public static string API_LOGIN { get; set; }
    public static string API_MIDTRANS { get; set; }
    
    [Obsolete]
    public App()
	{
		InitializeComponent();

        string publik = "https://php.ahlikoding.online/pos-accurate/";
        //string publik = "http://192.168.77.8/pos-accurate/";
        API_HOST = publik + "api/";
        
        API_LOGIN = publik + "config/mobile-login-api.php";
        API_MIDTRANS = publik + "midtrans/";

        UserAppTheme = AppTheme.Light;

        MainPage = new NavigationPage(new Sales.List_Faktur());

    }

    //protected override Window CreateWindow(IActivationState? activationState)
	//{
	//	//return new Window(new AppShell());
	//	return new Window(new Login());
	//}
}
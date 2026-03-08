using System.ComponentModel.DataAnnotations;

namespace InkVault.ViewModels
{
    public class WelcomeViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;
    }
}


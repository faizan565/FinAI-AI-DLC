using System.ComponentModel.DataAnnotations;

namespace FinAI.Web.Models;

public sealed class LoginViewModel
{
    [Required]
    [StringLength(256)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterViewModel
{
    [Required]
    [StringLength(256)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

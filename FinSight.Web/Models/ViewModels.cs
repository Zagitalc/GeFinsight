using System.ComponentModel.DataAnnotations;
using FinSight.Core.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinSight.Web.Models;

public class TransactionFormViewModel
{
    [Required]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "Category")]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    public TransactionType Type { get; set; } = TransactionType.Expense;

    [Range(0.01, 999999.99, ErrorMessage = "Enter an amount greater than 0.")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string Note { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class BudgetFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Category")]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Rule type")]
    public BudgetRuleType RuleType { get; set; } = BudgetRuleType.HardCap;

    [Range(0.01, 999999.99, ErrorMessage = "Enter a limit greater than 0.")]
    [Display(Name = "Limit")]
    public decimal LimitAmount { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
}

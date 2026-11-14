using BackendAba.Models;
using BackendAba.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAba.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController(PosStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<ExpenseRecord>> GetAll()
    {
        return Ok(store.GetExpenses());
    }

    [HttpPost]
    public ActionResult<ExpenseRecord> Register(RegisterExpenseRequest request)
    {
        return Ok(store.RegisterExpense(request));
    }
}

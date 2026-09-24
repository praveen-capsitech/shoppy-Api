using Microsoft.AspNetCore.Mvc.ModelBinding;
using MongoDB.Bson;

namespace ShoppyApp.Binding;

public sealed class MongoIdModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.HttpContext.Request.RouteValues[bindingContext.ModelName]?.ToString();

        if (ObjectId.TryParse(value, out var objectId))
        {
            bindingContext.Result = ModelBindingResult.Success(objectId.ToString());
        }
        else
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                "The value must be a valid MongoDB ObjectId.");
        }

        return Task.CompletedTask;
    }
}
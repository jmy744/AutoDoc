using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok();
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        if (id <= 0)
            return BadRequest("Invalid ID.");
        
        var todo = new { Id = id, Title = "Sample Todo", IsCompleted = false };
        
        if (todo == null)
            return NotFound($"Todo with ID {id} not found.");
        
        return Ok(todo);
    }

    [HttpPost]
    public IActionResult Create([FromBody] object todo)
    {
        if (todo == null)
            return BadRequest("Todo cannot be null.");
        
        return CreatedAtAction(nameof(GetById), new { id = 1 }, todo);
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] object todo)
    {
        if (id <= 0)
            return BadRequest("Invalid ID.");
        
        if (todo == null)
            return NotFound($"Todo with ID {id} not found.");
        
        return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        if (id <= 0)
            return BadRequest("Invalid ID.");
        
        return NoContent();
    }
}
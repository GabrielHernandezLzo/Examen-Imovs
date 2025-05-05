// MODELOS

using System.Data.Entity;
using System.Web.Http;

public class Producto
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public decimal PrecioUnitario { get; set; }
}

public class TicketDetalle
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal TotalFila => Cantidad * PrecioUnitario;
}

public class Ticket
{
    public int Id { get; set; }
    public string? Folio { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaLiquidacion { get; set; }
    public string Estatus { get; set; } = "Por pagar";

    public virtual ICollection<TicketDetalle>? Detalles { get; set; }
    public virtual ICollection<Pago>? Pagos { get; set; }

    public decimal Total => Detalles?.Sum(d => d.TotalFila) ?? 0;
    public decimal TotalPagado => Pagos?.Sum(p => p.Monto) ?? 0;
    public decimal Pendiente => Total - TotalPagado;
}

public class Pago
{
    public int Id { get; set; }
    public string? Folio { get; set; }
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public int NumeroPago { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public decimal Monto { get; set; }
}

// DB CONTEXT

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext() : base("DefaultConnection") { }

    public DbSet<Producto> Productos { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketDetalle> TicketDetalles { get; set; }
    public DbSet<Pago> Pagos { get; set; }

}

// CONTROLADORES

[RoutePrefix("api/productos")]
public class ProductoController : ApiController
{
    private readonly ApplicationDbContext db = new ApplicationDbContext();

    [HttpGet, Route("")]
    public IHttpActionResult GetAll() => Ok(db.Productos.ToList());

    [HttpGet, Route("{id:int}")]
    public IHttpActionResult Get(int id)
    {
        var producto = db.Productos.Find(id);
        if (producto == null) return NotFound();
        return Ok(producto);
    }

    [HttpPost, Route("")]
    public IHttpActionResult Create(Producto producto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        db.Productos.Add(producto);
        db.SaveChanges();
        return Ok(producto);
    }

    [HttpPut, Route("{id:int}")]
    public IHttpActionResult Update(int id, Producto producto)
    {
        var existing = db.Productos.Find(id);
        if (existing == null) return NotFound();

        existing.Nombre = producto.Nombre;
        existing.PrecioUnitario = producto.PrecioUnitario;

        db.SaveChanges();
        return Ok(existing);
    }

    [HttpDelete, Route("{id:int}")]
    public IHttpActionResult Delete(int id)
    {
        var producto = db.Productos.Find(id);
        if (producto == null) return NotFound();
        db.Productos.Remove(producto);
        db.SaveChanges();
        return Ok();
    }
}

[RoutePrefix("api/tickets")]
public class TicketController : ApiController
{
    private readonly ApplicationDbContext db = new ApplicationDbContext();

    [HttpGet, Route("")]
    public IHttpActionResult GetAll() =>
        Ok(db.Tickets.Include("Detalles.Producto").ToList());

    [HttpGet, Route("{id:int}")]
    public IHttpActionResult Get(int id)
    {
        var ticket = db.Tickets
            .Include("Detalles.Producto")
            .Include("Pagos")
            .FirstOrDefault(t => t.Id == id);

        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost, Route("")]
    public IHttpActionResult Create(Ticket ticket)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        ticket.FechaCreacion = DateTime.Now;
        ticket.Estatus = "Por pagar";

        db.Tickets.Add(ticket);
        db.SaveChanges();

        return Ok(ticket);
    }

    [HttpPut, Route("{id:int}")]
    public IHttpActionResult Update(int id, Ticket updated)
    {
        var existing = db.Tickets.Include("Detalles").FirstOrDefault(t => t.Id == id);
        if (existing == null) return NotFound();

        existing.Folio = updated.Folio;
        existing.Estatus = updated.Estatus;
        db.SaveChanges();

        return Ok(existing);
    }

    [HttpDelete, Route("{id:int}")]
    public IHttpActionResult Delete(int id)
    {
        var ticket = db.Tickets.Find(id);
        if (ticket == null) return NotFound();

        db.Tickets.Remove(ticket);
        db.SaveChanges();

        return Ok();
    }
}

[RoutePrefix("api/pagos")]
public class PagoController : ApiController
{
    private readonly ApplicationDbContext db = new ApplicationDbContext();

    [HttpGet, Route("ticket/{ticketId:int}")]
    public IHttpActionResult GetPagosPorTicket(int ticketId)
    {
        var pagos = db.Pagos
            .Where(p => p.TicketId == ticketId)
            .OrderByDescending(p => p.Fecha)
            .ToList();

        return Ok(pagos);
    }

    [HttpPost, Route("")]
    public IHttpActionResult Create(Pago pago)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ticket = db.Tickets.Include("Pagos").Include("Detalles")
            .FirstOrDefault(t => t.Id == pago.TicketId);

        if (ticket == null)
            return BadRequest("Ticket no encontrado.");

        pago.NumeroPago = ticket.Pagos.Count + 1;
        pago.Fecha = DateTime.Now;
        db.Pagos.Add(pago);
        db.SaveChanges();

        var totalPagado = ticket.Pagos.Sum(p => p.Monto) + pago.Monto;
        var totalTicket = ticket.Detalles.Sum(d => d.TotalFila);

        if (totalPagado >= totalTicket)
        {
            ticket.Estatus = "Pagado";
            ticket.FechaLiquidacion = DateTime.Now;
            db.SaveChanges();
        }

        return Ok(pago);
    }

    [HttpDelete, Route("{id:int}")]
    public IHttpActionResult Delete(int id)
    {
        var pago = db.Pagos.Find(id);
        if (pago == null) return NotFound();

        db.Pagos.Remove(pago);
        db.SaveChanges();

        return Ok();
    }
}

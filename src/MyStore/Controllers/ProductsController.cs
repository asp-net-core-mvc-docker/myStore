using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyStore.Framework;
using MyStore.Models;
using MyStore.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Filters;
using SixLabors.ImageSharp.Processing.Transforms;


namespace MyStore.Controllers
{
    public class ProductsController : BaseController
    {
        private readonly IProductService _productService;
        private readonly IFileService _fileService;

        public ProductsController(IProductService productService, IFileService fileService)
        {
            _productService = productService;
            _fileService = fileService;
        }

        [HttpGet("browse")]
        public async Task<IActionResult> Browse(string name)
        {
            Guid userGuid;
            Guid.TryParse(this.User.FindFirstValue(ClaimTypes.NameIdentifier),  out userGuid);

            var products = await _productService.BrowseAsync(name);
            var viewModels = products.Select(p =>
                new ProductViewModel
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    Name = p.Name,
                    Category = p.Category,
                    Price = p.Price
                });
            if (userGuid != Guid.Empty)
                viewModels = viewModels.Where(c => c.UserId == userGuid);

            return View(viewModels);
        }

        [HttpGet("{id}/details")]
        public async Task<IActionResult> Details(Guid id)
        {
            var product = await _productService.GetAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var viewModel = new ProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category,
                Price = product.Price
            };

            return View(viewModel);
        }

        [HttpGet("create")]
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public IActionResult Create()
        {

            
            var viewModel = new CreateProductViewModel();
            
            return View(viewModel);
        }

        [HttpPost("create")]
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Create(CreateProductViewModel viewModel)
        {
            Guid userId = new Guid(this.User.FindFirstValue(ClaimTypes.NameIdentifier));
            await _productService.CreateAsync(Guid.NewGuid(), userId, viewModel.Name,
                viewModel.Category, viewModel.Price);

            return RedirectToAction(nameof(Browse));
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BrowseProducts query)
        {
            var products = await _productService.BrowseAsync(query.Name);

            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var product = await _productService.GetAsync(id);
            if (product != null)
            {
                return Ok(product);
            }

            return NotFound();
        }

        [HttpPost]
        [ModelValidationFilter]
        public async Task<IActionResult> Post([FromBody] CreateProduct request)
        {
            await _productService.CreateAsync(Guid.NewGuid(), request.UserId, request.Name,
                request.Category, request.Price);

            return Ok();
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(ICollection<IFormFile> files)
        {
            Guid userGuid;
            Guid.TryParse(this.User.FindFirstValue(ClaimTypes.NameIdentifier), out userGuid);
            List<string> pathImage = new List<string>();

            var filesPath = Environment.GetEnvironmentVariable("FILES_DIR");
            foreach (var file in files)
            {
                Guid fileNameGuid = Guid.NewGuid();
                if (file.Length > 0)
                {
                    //using (var fileStream = new FileStream(Path.Combine($"{filesPath}", file.FileName), FileMode.Create))
                    using (var fileStream = new FileStream(Path.Combine($"{filesPath}", fileNameGuid.ToString() + Path.GetExtension(file.FileName)), FileMode.OpenOrCreate))
                    {
                        pathImage.Add(fileNameGuid.ToString() + Path.GetExtension(file.FileName));
                        await file.CopyToAsync(fileStream);    
                    }
                }
            }

            foreach (var imageName in pathImage)
            {
                using (var stream = new FileStream(filesPath + "\\" + imageName, FileMode.Open))
                {
                    using (Image<Rgba32> image = SixLabors.ImageSharp.Image.Load(stream))
                    {
                        if (image.Width > image.Height)
                        {
                            image.Mutate(x => x
                             .Resize(120, 80));
                        }
                        else
                        {
                            image.Mutate(x => x
                            .Resize(80, 120));
                        }

                        using (var minFileStream = new FileStream(filesPath + "\\" + "min_" + imageName, FileMode.Create))
                        {
                            image.SaveAsPng(minFileStream);
                        }
                    }
                }
            }

            return RedirectToAction(nameof(Create)); 
        }
    }

    public class CreateProduct
    {
        [Required]
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
    }

    public class BrowseProducts
    {
        public string Name { get; set; }
    }
}
﻿using FairyTale.API.Data;
using FairyTale.API.Models;
using FairyTale.API.Models.DTOs;
using FairyTale.API.Models.DTOs.DwarfDTOs;
using FairyTale.API.Models.DTOs.SnowWhiteDTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FairyTale.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SnowWhiteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SnowWhiteController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<SnowWhiteDTO>))]
        public async Task<IActionResult> Get()
        {
            var userId = Convert.ToInt32(User.Claims.FirstOrDefault(x => x.Type == ClaimsIdentity.DefaultNameClaimType)?.Value);

            return new JsonResult(await _context.SnowWhites
                .Where(x=> x.Id != userId)
                .Select(x => new SnowWhiteDTO
                {
                    FullName = x.FullName,
                    Id = x.Id,
                }).ToArrayAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SnowWhiteDetailsDTO))]
        public async Task<IActionResult> Get(int id)
        {
            return new JsonResult(await _context.SnowWhites
                .Select(x => new SnowWhiteDetailsDTO
                {
                    FullName = x.FullName,
                    Id = x.Id,
                    Dwarves = x.Dwarfs.Select(x => new DwarfDTO
                    {
                        Class = x.Class,
                        Id = x.Id,
                        Name = x.Name,
                    })
                }).FirstOrDefaultAsync(x => x.Id == id));
        }

        [HttpPut]
        public async Task<IActionResult> Edit(SnowWhiteEditDTO model)
        {
            var userId = Convert.ToInt32(User.Claims.FirstOrDefault(x => x.Type == ClaimsIdentity.DefaultNameClaimType)?.Value);

            if (userId != model.Id)
                return Forbid();

            var snowWhite = await _context.SnowWhites.SingleOrDefaultAsync(x => x.Id == model.Id);
            if (snowWhite == null)
                return StatusCode(StatusCodes.Status404NotFound);

            var snowWhiteExists = await _context.SnowWhites.AnyAsync(x => x.Id != model.Id && x.FullName == model.FullName);

            if (snowWhiteExists)
                return StatusCode(StatusCodes.Status404NotFound);

            snowWhite.FullName = model.FullName;

            _context.SnowWhites.Update(snowWhite);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status204NoContent);
        }

        [HttpGet("dwarfs")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DwarfDTO))]
        public async Task<IActionResult> GetDwarfs()
        {
            var userId = Convert.ToInt32(User.Claims.FirstOrDefault(x => x.Type == ClaimsIdentity.DefaultNameClaimType)?.Value);
            var dwarves = await _context.Dwarfs.Where(x => x.SnowWhiteId == userId)
                .Select(x => new DwarfDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Class = x.Class
                })
                .ToArrayAsync();

            return new JsonResult(dwarves);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetRequests()
        {
            var userId = Convert.ToInt32(User.Claims.FirstOrDefault(x => x.Type == ClaimsIdentity.DefaultNameClaimType)?.Value);
            var requests = await _context.Requests.Where(x => x.DungeonMasterSnowWhiteId == userId)
                .Select(x => new RequestDTO
                {
                    Id = x.Id,
                    DwarfName = x.Dwarf.Name,
                    SnowWhiteFullName = x.CreatedRequestSnowWhite.FullName
                })
                .ToArrayAsync();

            return Ok(requests);
        }

        [HttpHead("requests/{requestId}/answer")]
        public async Task<IActionResult> AnswerRequest(int requestId, bool accept)
        {
            var userId = Convert.ToInt32(User.Claims.FirstOrDefault(x => x.Type == ClaimsIdentity.DefaultNameClaimType)?.Value);
            var request = await _context.Requests.SingleOrDefaultAsync(x => x.Id == requestId);
            if (request == null)
                return NotFound();

            var dwarf = await _context.Dwarfs.SingleAsync(x => x.Id == request.DwarfId);

            if (userId != dwarf.SnowWhiteId || request.IsClosed)
                return Forbid();

            request.IsClosed = true;
            _context.Update(request);
            await _context.SaveChangesAsync();

            if (!accept)
                return Ok();

            dwarf.SnowWhiteId = request.CreatedRequestSnowWhiteId.Value;
            _context.Update(dwarf);
            
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}

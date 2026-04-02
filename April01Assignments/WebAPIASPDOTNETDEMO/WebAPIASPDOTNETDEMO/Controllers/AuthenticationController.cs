using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebAPIASPDOTNETDEMO.Models;

namespace WebAPIASPDOTNETDEMO.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly IConfiguration _configuration;

        public AuthenticationController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;

            _configuration = configuration;
        }


        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterUser registerUser, string role)
        {
            //Check User Exist 
            var userExist = await _userManager.FindByEmailAsync(registerUser.Email);
            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new Response { Status = "Error", Message = "User already exists!" });
            }

            //Add the User in the database
            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.Username

            };
            if (await _roleManager.RoleExistsAsync(role))
            {
                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "User Failed to Create" });
                }
                //Add role to the user....

                await _userManager.AddToRoleAsync(user, role);
                return StatusCode(StatusCodes.Status200OK,
                    new Response { Status = "Success", Message = $"User created SuccessFully" });

            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "This Role Doesnot Exist." });
            }


        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddYears(2),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return token;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            var user = await _userManager.FindByNameAsync(loginModel.Username);

            if (user != null && await _userManager.CheckPasswordAsync(user, loginModel.Password))
            {
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };
                var userRoles = await _userManager.GetRolesAsync(user);
                foreach (var role in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }


                var jwtToken = GetToken(authClaims);

                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                    expiration = jwtToken.ValidTo
                });
                //returning the token...

            }
            return Unauthorized();
        }
        [HttpPost]
        [Route("logout")]
        public IActionResult Logout()
        {
            return Ok(new Response { Status = "Success", Message = "Logged out successfully" });
        }
    }
}

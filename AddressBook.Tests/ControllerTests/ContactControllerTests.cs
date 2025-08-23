using AddressBook.Api.Controllers;
using AddressBook.Application;
using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Castle.Core.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AddressBook.Tests.ControllerTests
{
    public class ContactControllerTests
    {
        public class ContactsControllerTests
        {
            private readonly Mock<IContactService> _mockContactService;
            private readonly Mock<ILogger<ContactsController>> _mockLogger;
            private readonly ContactsController _controller;

            public ContactsControllerTests()
            {
                _mockContactService = new Mock<IContactService>();
                _mockLogger = new Mock<ILogger<ContactsController>>();
                _controller = new ContactsController(_mockContactService.Object, _mockLogger.Object);
            }

            #region UpsertContact

            [Fact]
            public async Task UpsertContact_NullContact_ReturnsBadRequest()
            {
                // Arrange
                ContactDto contactDto = null;

                // Act
                var result = await _controller.UpsertContact(contactDto);

                // Assert
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Equal("Contact data is required.", badRequestResult.Value);
            }

            [Fact]
            public async Task UpsertContact_NewContact_ReturnsCreated() 
            {
                // Arrange
                var contactDto = new ContactDto { FirstName = "John", LastName = "Doe" };
                _mockContactService.Setup(s => s.UpsertContactAsync(contactDto))
                    .ReturnsAsync(new UpsertContactResult { Status = UpsertStatus.Created, ContactDto = contactDto });

                // Act
                var result = await _controller.UpsertContact(contactDto);

                // Assert
                var createdResult = Assert.IsType<CreatedAtActionResult>(result);
                Assert.Equal(contactDto, createdResult.Value);
            }

            [Fact]
            public async Task UpsertContact_ExistingContact_ReturnsOk() 
            {
                // Arrange
                var contactDto = new ContactDto { FirstName = "Jane", LastName = "Smith" };
                _mockContactService.Setup(s => s.UpsertContactAsync(contactDto))
                    .ReturnsAsync(new UpsertContactResult { Status = UpsertStatus.Updated, ContactDto = contactDto });

                // Act
                var result = await _controller.UpsertContact(contactDto);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result);
                Assert.Equal(contactDto, okResult.Value);
            }

            [Fact]
            public async Task UpsertContact_ServiceThrowsException_Returns500()
            {
                // Arrange
                var mockService = new Mock<IContactService>();

                var sampleDto = new ContactDto
                {
                    Id = Guid.NewGuid(),
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john@example.com",
                    PhoneNumber = "123-456"
                };

                // Setup the service to throw an exception
                mockService.Setup(s => s.UpsertContactAsync(It.IsAny<ContactDto>()))
                           .ThrowsAsync(new Exception("Database failure"));

                var controller = new ContactsController(mockService.Object, _mockLogger.Object);

                // Act
                var result = await controller.UpsertContact(sampleDto);

                // Assert
                var objectResult = Assert.IsType<ObjectResult>(result);
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
                Assert.Contains("An unexpected error occurred", objectResult?.Value?.ToString());
                Assert.Contains("UpsertContact", objectResult?.Value?.ToString());
            } 

            #endregion


            #region GetContactById

            [Fact]
            public async Task GetContactById_InvalidId_ReturnsBadRequest() 
            {
                // Act
                var result = await _controller.GetContactById(Guid.Empty);

                // Assert
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Equal("Invalid contact id.", badRequest.Value);
            }

            [Fact]
            public async Task GetContactById_NotFound_ReturnsNotFound() 
            {
                // Arrange
                var id = Guid.NewGuid();
                _mockContactService.Setup(s => s.GetContactByIdAsync(id)).ReturnsAsync((ContactDto)null);

                // Act
                var result = await _controller.GetContactById(id);

                // Assert
                var notFound = Assert.IsType<NotFoundObjectResult>(result);
                Assert.Equal("Contact with id not found.", notFound.Value);
            }

            [Fact]
            public async Task GetContactById_ValidId_ReturnsOk() 
            {
                // Arrange
                var id = Guid.NewGuid();
                var contactDto = new ContactDto { Id = id, FirstName = "John", LastName = "Doe", PhoneNumber="012322323", Email="johndoe@gmail.com"};
                _mockContactService.Setup(s => s.GetContactByIdAsync(id)).ReturnsAsync(contactDto);

                // Act
                var result = await _controller.GetContactById(id);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result);
                Assert.Equal(contactDto, okResult.Value);
            }

            [Fact]
            public async Task GetContactById_ServiceThrowsException_Returns500()
            {
                // Arrange
                var contactId = Guid.NewGuid();

                // Setup the service to throw an exception
                _mockContactService.Setup(s => s.GetContactByIdAsync(It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception("An unexpected error occurred in GetContactById"));

                var controller = new ContactsController(_mockContactService.Object, _mockLogger.Object);

                // Act
                var result = await controller.GetContactById(contactId);

                // Assert
                var objectResult = Assert.IsType<ObjectResult>(result);
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
                Assert.Contains("An unexpected error occurred", objectResult?.Value?.ToString());
                Assert.Contains("GetContactById", objectResult?.Value?.ToString());
            }


            #endregion



            #region GetContactList


            [Fact]
            public async Task GetContactList_InvalidPagination_ReturnsBadRequest() 
            {
                // Act
                var result = await _controller.GetContactList(0, 0);

                // Assert
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Equal("Page and pageSize must be greater than zero.", badRequest.Value);
            }
         
            [Fact]
            public async Task GetContactList_ReturnsOkWithContacts() 
            {
                // Arrange
                var contacts = new List<ContactDto>
            {
                new ContactDto { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe" },
                new ContactDto { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Smith" }
            };

                _mockContactService.Setup(s => s.GetContactListAsync(1, 10))
                    .ReturnsAsync((contacts, contacts.Count));

                // Initialize ControllerContext to allow Response.Headers usage
                _controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        Response = { Body = new MemoryStream() }
                    }
                };

                // Act
                var result = await _controller.GetContactList(1, 10);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result);
                Assert.Equal(contacts, okResult.Value);
            }

            [Fact]
            public async Task GetContactList_ServiceThrowsException_Returns500()
            {
                // Arrange
                int page = 1;
                int pageSize = 10;

                // Setup the service to throw an exception
                _mockContactService.Setup(s => s.GetContactListAsync(page, pageSize))
                           .ThrowsAsync(new Exception("Database failure"));

                var controller = new ContactsController(_mockContactService.Object, _mockLogger.Object)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext()
                    }
                };

                // Act
                var result = await controller.GetContactList(page, pageSize);

                // Assert
                var objectResult = Assert.IsType<ObjectResult>(result);
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
                Assert.Contains("An unexpected error occurred while fetching contacts", objectResult?.Value?.ToString());
                Assert.Contains("GetContactList", objectResult?.Value?.ToString());
            } 

            #endregion
        }
    }
}

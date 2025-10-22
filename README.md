# Contract Monthly Claim System (CMCS) – Part 2



This project is a web-based system built using ASP.NET Core MVC. It allows lecturers to submit monthly claims, upload supporting documents and track the status of their submissions. Coordinators and managers can review, approve or reject these claims.



### YouTube Link



### Setup Instructions



1. Clone the repository.
   
2. Open the solution in Visual Studio 2022.
   
3. Ensure that the .NET 9.0 SDK is installed.
   
4. Check that appsettings.json contains a valid 32-byte AES key under "Security:EncryptionKeyBase64".
   
5. Build the project to restore all dependencies.
   
6. Run the application.
   
7. Open the web browser and go to https://localhost:xxxx.



#### Login Details

Lecturer – password: lecturer

Coordinator – password: coordinator

Manager – password: manager



### Pages and Views

• Home/Index – Login page where users select a role and enter the corresponding password.

• Lecturers/Index – Lecturer dashboard showing the lecturer name and ID.

• Lecturers/Create – Submit a new claim by entering hours, rate and notes (total calculated automatically).

• Lecturers/Details – View all submitted claims, approval status, and uploaded documents.

• Lecturers/UploadDocument – Upload PDF, DOCX or XLSX files related to a claim.

• Coordinator/Index – Coordinator landing page.

• Coordinator/Claims – View all lecturer claims and approve or reject them.

• Coordinator/Edit – Review an individual claim, leave comments and make a decision.

• Manager/Index – Manager landing page.

• Manager/Claims – View and manage all claims.

• Manager/Edit – Review and decide on an individual claim.



### Technologies Used

• ASP.NET Core MVC

• C#

• Bootstrap (for styling)

• AES-256 encryption for file storage

• JSON persistence for application data



Data and Security

• Data is stored in memory using InMemoryStore and saved to a JSON file (App\_Data/CMCSPart2-state.json).

• Uploaded documents are encrypted using AES-256 in CBC mode and stored under App\_Data/supporting-docs.

• Files are decrypted automatically when downloaded.

• Each lecturer is assigned a unique LecturerId.

• Claims include details such as hours worked, hourly rate, total, and approval history.



### Unit Tests

The solution includes automated tests under the CMCSPart2Tests project. These tests confirm that the system behaves correctly:



#### StoreBasicsTests.cs

Verifies that when a new user logs in as a lecturer, they are assigned a UserId and a corresponding LecturerId.

Ensures that the lecturer is correctly linked to the user and that repeated creation returns the same lecturer record.



#### ClaimsWorkflowTests.cs

Tests that when a claim is created, the total amount is calculated correctly and the claim starts with a “Pending” status.

Also confirms that when a coordinator or manager approves a claim, the status and approval record are updated properly with the decision, approver role and date.



#### DocumentSecurityTests.cs

Checks that files uploaded through the system are encrypted at rest and can be successfully decrypted.

Verifies that the encrypted file on disk differs in size from the original file and that decryption restores the original content exactly.



#### PersistenceSnapshotTests.cs

Ensures that when the application saves data, all claims, lecturers, and users persist correctly across restarts.

Confirms that after reloading the persisted snapshot, claims remain intact with the same data and status.



#### TestHelpers.cs (with FakeEnv and TestPaths)

Provides helper methods for creating temporary test environments and directories to isolate test runs from local data.



### Configuration File (appsettings.json)

#### Security:

• EncryptionKeyBase64 – 32-byte key for AES encryption.

• PrivateUploadsFolder – location where encrypted files are stored.



#### Persistence:

• Enabled – enables saving of application state.

• DataFile – path to JSON file storing in-memory data.

• EncryptState – whether to encrypt the JSON snapshot itself.



### Notes

• The system does not use a database and all information is managed in memory and persisted to disk as JSON.

• The approval log shows the role (Coordinator/Manager) rather than usernames to indicate who made the decision.

• Each lecturer’s claims and uploaded documents remain private and linked via their LecturerId.



### References

Ctrl+Alt+Teach (n.d.) Unit Test Tutorials MVC. YouTube video. Available at: https://www.youtube.com/watch?v=WCX1IXUo0ho (Accessed: 22 October 2025)

Digital TechJoint (n.d.) Implementing AES 256 Encryption in ASP.NET – Step-by-Step Tutorial. YouTube video. Available at: https://www.youtube.com/watch?v=LctYdd-fen8 (Accessed: 22 October 2025)

Microsoft Docs. (n.d.) Unit testing C# in .NET using dotnet test and xUnit. Available at: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit (Accessed: 22 October 2025)

Troelsen, A. and Japikse, P., 2022. Pro C# 10 with .NET 6: Foundational Principles and Practices in Programming. 11th ed. Berkeley, CA: Apress








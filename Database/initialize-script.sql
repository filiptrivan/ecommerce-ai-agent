-- These permissions will be assigned to the first registered user in the application.

begin transaction;

use EcommerceAIAgent

insert into Permission(Name, Description, Code) values(N'View users', null, N'ReadUser');
insert into Permission(Name, Description, Code) values(N'Edit existing users', null, N'UpdateUser');
insert into Permission(Name, Description, Code) values(N'Delete users', null, N'DeleteUser');
insert into Permission(Name, Description, Code) values(N'View notifications', null, N'ReadNotification');
insert into Permission(Name, Description, Code) values(N'Edit existing notifications', null, N'UpdateNotification');
insert into Permission(Name, Description, Code) values(N'Add new notifications', null, N'InsertNotification');
insert into Permission(Name, Description, Code) values(N'Delete notifications', null, N'DeleteNotification');
insert into Permission(Name, Description, Code) values(N'View roles', null, N'ReadRole');
insert into Permission(Name, Description, Code) values(N'Edit existing roles', null, N'UpdateRole');
insert into Permission(Name, Description, Code) values(N'Add new roles', null, N'InsertRole');
insert into Permission(Name, Description, Code) values(N'Delete roles', null, N'DeleteRole');

INSERT INTO Role (Version, Name, CreatedAt, ModifiedAt) VALUES (1, N'Admin', getdate(), getdate());

DECLARE @AdminRoleId INT;
DECLARE @AdminUserId INT;
SELECT @AdminRoleId = Id FROM Role WHERE Name = N'Admin';
SELECT TOP 1 @AdminUserId = Id FROM [User] ORDER BY Id;

INSERT INTO UserRole (UserId, RoleId) VALUES (@AdminUserId, @AdminRoleId);

INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 1);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 2);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 3);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 4);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 5);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 6);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 7);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 8);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 9);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 10);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 11);

commit;

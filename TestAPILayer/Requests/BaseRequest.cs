namespace TestAPILayer.Requests
{

    public class BaseRequest
    {
        // Files
        public const string CreateFolder = "CreateFolder";
        public const string CreateFile = "CreateFile";
        public const string CreateVersion = "CreateVersion";
        public const string ListFiles = "ListFiles";
        public const string GetNodeKey = "GetNodeKey";
        public const string OpenFile = "OpenFile";
        public const string OpenPreviousVersion = "OpenPreviousVersion";
        public const string Rename = "Rename";
        public const string Delete = "Delete";
        public const string Move = "Move";
        public const string GetNumberOfNodes = "GetNumberOfNodes";
        public const string Copy = "Copy";
        public const string GetFileInfo = "GetFileInfo";
        public const string AddReview = "AddReview";
        public const string GetReview = "GetReview";
        public const string GetVersionList = "GetVersionList";
        public const string SetControlledFlag = "SetControlledFlag";
        public const string AddFileTag = "AddFileTag";
        public const string RemoveFileTag = "RemoveFileTag";
        public const string SearchByTag = "SearchByTag";
        public const string GetFileActions = "GetFileActions";
        public const string RemoveReview = "RemoveReview";
        public const string Approval = "Approval";
        public const string RevokeApproval = "RevokeApproval";

        // Register
        public const string CreateUser = "CreateUser";
        public const string InviteUser = "InviteUser";
        public const string RegisterUser = "RegisterUser";
        public const string ActivateUser = "ActivateUser";
        public const string DeactivateUser = "DeactivateUser";
        public const string DeleteUser = "DeleteUser";

        public const string CreateDevice = "CreateDevice";
        public const string RegisterDevice = "RegisterDevice";
        public const string CreateOrg = "CreateOrg";
        public const string InviteOrg = "InviteOrg";
        public const string RegisterOrg = "RegisterOrg";
        public const string CreateUserAndDevice = "CreateUserAndDevice";
        public const string RegisterUserAndDevice = "RegisterUserAndDevice";
        public const string SendNewUserAndDeviceCode = "SendNewUserAndDeviceCode";
        public const string ActivateDevice = "ActivateDevice";
        public const string DeactivateDevice = "DeactivateDevice";
        public const string RevokeDevice = "RevokeDevice";
        public const string GetAttribute = "GetAttribute";

        // Account
        public const string ChangeUserPassword = "ChangeUserPassword";
        public const string Login = "Login";
        public const string ListDevices = "ListDevices";

        // Permissions
        public const string SharedWith = "SharedWith";
        public const string CreateGroup = "CreateGroup";
        public const string GetGroup = "GetGroup";
        public const string RenameGroup = "RenameGroup";
        public const string RemoveGroup = "RemoveGroup";
        public const string AddUserToGroup = "AddUserToGroup";
        public const string RemoveUserFromGroup = "RemoveUserFromGroup";
        public const string GrantAccess = "GrantAccess";
        public const string RevokeAccess = "RevokeAccess";
        public const string AddPermissions = "AddPermissions";
        public const string RemovePermission = "RemovePermission";
        public const string DisableInherit = "DisableInherit";
        public const string EnableInherit = "EnableInherit";
        public const string ListGroups = "ListGroups";
        public const string ActivateGroup = "ActivateGroup";
        public const string DeactivateGroup = "DeactivateGroup";
        public const string DeleteGroup = "DeleteGroup";
        public const string GetPermissionTypes = "GetPermissionTypes";
        public const string ListNoAccess = "ListNoAccess";
        public const string CanCreate = "CanCreate";
        public const string CanUpdate = "CanUpdate";
        public const string CanDelete = "CanDelete";
        public const string CanShare = "CanShare";

        // Users
        public const string GetProfileImage = "GetProfileImage";
        public const string GetUser = "GetUser";
        public const string ListUsers = "ListUsers";
        public const string GetUserActions = "GetUserActions";

        public string TYP { get; set; }
    }
}

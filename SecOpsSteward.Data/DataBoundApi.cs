using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecOpsSteward.Data.Models;
using SecOpsSteward.Data.Workflow;
using SecOpsSteward.Shared;
using SecOpsSteward.Shared.Packaging;
using SecOpsSteward.Shared.Roles;

namespace SecOpsSteward.Data
{
    public class DataBoundApi
    {
        private readonly ChimeraSystemOperationsService _chimeraSystem;
        private readonly PackageActionsService _packageActionsService;
        private readonly IPackageRepository _packageRepository;
        private readonly IRoleAssignmentService _roleAssignment;
        private readonly IDbContextFactory<SecOpsStewardDbContext> _dbFactory;

        public DataBoundApi(
            ChimeraSystemOperationsService chimeraSystem,
            IRoleAssignmentService roleAssignment,
            PackageActionsService packageActionsService,
            IPackageRepository packageRepository,
            IDbContextFactory<SecOpsStewardDbContext> dbFactory)
        {
            _chimeraSystem = chimeraSystem;
            _roleAssignment = roleAssignment;
            _packageActionsService = packageActionsService;
            _packageRepository = packageRepository;
            _dbFactory = dbFactory;
        }

        public async Task<UserModel> AddUser(TokenOwner tokenOwner, ChimeraUserRole role)
        {
            await _chimeraSystem.CreateUser(tokenOwner.UserId, role);

            var userModel = new UserModel
            {
                UserId = tokenOwner.UserId.Id,
                DisplayName = tokenOwner.Name,
                Username = string.Join(',', tokenOwner.Aliases.Append(tokenOwner.Email))
            };

            using (var cxt = _dbFactory.CreateDbContext())
            {
                cxt.Users.Add(userModel);
                await cxt.SaveChangesAsync();
            }

            return userModel;
        }

        public async Task<UserModel> AddUser(string username, ChimeraUserRole role)
        {
            var user = await _roleAssignment.ResolveUsername(username);
            await _chimeraSystem.CreateUser(user.UserId, role);

            var userModel = new UserModel
            {
                UserId = user.UserId.Id,
                DisplayName = user.Name,
                Username = user.Email,
                Role = role
            };

            using (var cxt = _dbFactory.CreateDbContext())
            {
                cxt.Users.Add(userModel);
                await cxt.SaveChangesAsync();
            }

            return userModel;
        }

        public async Task RemoveUser(Guid userId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var userModel = cxt.Users.FirstOrDefault(u => u.UserId == userId);
                if (userModel != null)
                {
                    await _chimeraSystem.DestroyUser(userId, userModel.Role);
                    cxt.Users.Remove(userModel);
                    await cxt.SaveChangesAsync();
                }
            }
        }

        public async Task CheckAgentRights(Guid grantingUserId, Guid workflowId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var wf = cxt.Workflows.FirstOrDefault(w => w.WorkflowId == workflowId);

                await Task.WhenAll(wf.SavedData.Nodes.Select(step =>
                {
                    return CheckAgentRights(grantingUserId, workflowId, step.WorkflowStepId);
                }));
            }
        }

        public async Task CheckAgentRights(Guid grantingUserId, Guid workflowId, Guid workflowStepId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var node = GetWorkflowNode(workflowId, workflowStepId);
                var pkg = cxt.Plugins.First(p => p.PluginId == node.PackageId);

                var hashCode = node.Parameters.GetConfigurationGrantScopeHashCode();

                var agent = cxt.Agents.First(a => a.AgentId == node.AgentId);

                var result = await _packageActionsService.HasAccess(new ChimeraPackageIdentifier(node.PackageId),
                    node.Parameters, agent.Identity);

                var agentGrantRecord = cxt.AgentGrants.FirstOrDefault(ag =>
                    ag.AgentId == node.AgentId && ag.AuthorizationScopeHashcode == hashCode);

                // if grant is present in DB but is invalid, drop it
                if (agentGrantRecord != null && !result) cxt.AgentGrants.Remove(agentGrantRecord);

                // if grant is missing from DB but exists, add it
                if (agentGrantRecord == null && result)
                    cxt.AgentGrants.Add(new AgentGrantModel
                    {
                        AgentId = node.AgentId,
                        AuthorizationScopeHashcode = hashCode,
                        PluginId = node.PackageId,
                        UserPerformingGrantId = grantingUserId
                    });

                await cxt.SaveChangesAsync();
            }
        }

        public async Task GrantAgentRightsToRun(Guid grantingUserId, Guid workflowId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var wf = cxt.Workflows.FirstOrDefault(w => w.WorkflowId == workflowId);

                await Task.WhenAll(wf.SavedData.Nodes.Select(step =>
                {
                    return GrantAgentRightsToRun(grantingUserId, workflowId, step.WorkflowStepId);
                }));
            }
        }

        public async Task GrantAgentRightsToRun(Guid grantingUserId, Guid workflowId, Guid workflowStepId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var node = GetWorkflowNode(workflowId, workflowStepId);
                var pkg = cxt.Plugins.First(p => p.PluginId == node.PackageId);

                var hashCode = node.Parameters.GetConfigurationGrantScopeHashCode();

                var agent = cxt.Agents.First(a => a.AgentId == node.AgentId);

                if (cxt.AgentGrants.Any(ag =>
                    ag.AgentId == node.AgentId && ag.PluginId == node.PackageId &&
                    ag.AuthorizationScopeHashcode == hashCode))
                    return; // exists

                if (!await _packageActionsService.HasAccess(new ChimeraPackageIdentifier(node.PackageId),
                    node.Parameters, agent.Identity))
                    await _packageActionsService.Grant(new ChimeraPackageIdentifier(node.PackageId), node.Parameters,
                        agent.Identity);

                cxt.AgentGrants.Add(new AgentGrantModel
                {
                    AgentId = node.AgentId,
                    AuthorizationScopeHashcode = hashCode,
                    PluginId = node.PackageId,
                    UserPerformingGrantId = grantingUserId
                });

                await cxt.SaveChangesAsync();
            }
        }

        public async Task RevokeAgentRightsToRun(Guid grantingUserId, Guid workflowId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var wf = cxt.Workflows.FirstOrDefault(w => w.WorkflowId == workflowId);

                await Task.WhenAll(wf.SavedData.Nodes.Select(step =>
                {
                    return RevokeAgentRightsToRun(grantingUserId, workflowId, step.WorkflowStepId);
                }));
            }
        }

        public async Task RevokeAgentRightsToRun(Guid grantingUserId, Guid workflowId, Guid workflowStepId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var node = GetWorkflowNode(workflowId, workflowStepId);
                var pkg = cxt.Plugins.First(p => p.PluginId == node.PackageId);

                var hashCode = node.Parameters.GetConfigurationGrantScopeHashCode();

                var correspondingRecord = cxt.AgentGrants.First(ag =>
                    ag.AgentId == node.AgentId && ag.AuthorizationScopeHashcode == hashCode);

                var agent = cxt.Agents.First(a => a.AgentId == node.AgentId);

                if (!cxt.AgentGrants.Any(ag => ag.AgentId == node.AgentId && ag.AuthorizationScopeHashcode == hashCode))
                    return; // does not exist

                await _packageActionsService.Revoke(new ChimeraPackageIdentifier(node.PackageId), node.Parameters,
                    agent.Identity);

                cxt.AgentGrants.Remove(correspondingRecord);
                await cxt.SaveChangesAsync();
            }
        }

        public async Task Grant(Guid agentId, Guid userId, Guid packageId)
        {
            await _chimeraSystem.WithConfiguration(agentId, c => { c.AccessRules.Add(userId, packageId); });

            using (var cxt = _dbFactory.CreateDbContext())
            {
                cxt.AgentPermissions.Add(new AgentPermissionModel
                {
                    AgentId = agentId,
                    UserId = userId,
                    PackageId = packageId
                });
                await cxt.SaveChangesAsync();
            }
        }

        public async Task Revoke(Guid agentId, Guid userId, Guid packageId)
        {
            await _chimeraSystem.WithConfiguration(agentId, c => { c.AccessRules.Remove(userId, packageId); });

            using (var cxt = _dbFactory.CreateDbContext())
            {
                var perm = cxt.AgentPermissions.FirstOrDefault(p =>
                    p.AgentId == agentId &&
                    p.UserId == userId &&
                    p.PackageId == packageId);
                if (perm != null) cxt.AgentPermissions.Remove(perm);
                await cxt.SaveChangesAsync();
            }
        }

        public async Task<AgentModel> AddAgent(string tag, Guid newId)
        {
            await _chimeraSystem.CreateAgent(newId);

            var agentModel = new AgentModel
            {
                AgentId = newId,
                Identity = newId.ToString(),
                Enabled = true
            };

            using (var cxt = _dbFactory.CreateDbContext())
            {
                cxt.Agents.Add(agentModel);
                await cxt.SaveChangesAsync();
            }

            await ChangeTag(newId, tag);

            return agentModel;
        }

        public async Task ChangeAgentIdentity(Guid agentId, string identity)
        {
            using (var dbContext = _dbFactory.CreateDbContext())
            {
                var agentModel = dbContext.Agents.First(a => a.AgentId == agentId);
                agentModel.Identity = identity;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task ChangeTag(Guid agentId, string newTag)
        {
            await _chimeraSystem.WithConfiguration(agentId, a => { a.DisplayAlias = newTag; });

            using (var dbContext = _dbFactory.CreateDbContext())
            {
                var agentModel = dbContext.Agents.First(a => a.AgentId == agentId);
                agentModel.Tag = newTag;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task RemoveAgent(Guid agentId)
        {
            await _chimeraSystem.DestroyAgent(agentId);

            using (var cxt = _dbFactory.CreateDbContext())
            {
                var agentModel = cxt.Agents.FirstOrDefault(a => a.AgentId == agentId);
                if (agentModel != null) cxt.Agents.Remove(agentModel);
                await cxt.SaveChangesAsync();
            }
        }

        public async Task<List<PluginMetadataModel>> AddPackage(ChimeraContainer package)
        {
            await _packageRepository.CreateOrUpdate(package);
            var metadata = package.GetMetadata();

            using (var cxt = _dbFactory.CreateDbContext())
            {
                // create plugins and services
                var containerModel = ContainerModel.FromMetadata(metadata);
                cxt.Add(containerModel);

                await cxt.SaveChangesAsync();

                return containerModel.ManagedServices.SelectMany(s => s.Plugins).ToList();
            }
        }

        public Task<List<PluginMetadataModel>> AddContainer(Stream packageZip, bool disposeStreamOnClose = true)
        {
            return AddPackage(new ChimeraContainer(packageZip, disposeStreamOnClose));
        }

        public async Task RemovePackage(Guid containerId)
        {
            await _packageRepository.Delete(containerId);

            using (var cxt = _dbFactory.CreateDbContext())
            {
                var containerModel = cxt.Containers.FirstOrDefault(c => c.ContainerId == containerId);
                if (containerModel != null) cxt.Containers.Remove(containerModel);
                await cxt.SaveChangesAsync();
            }
        }

        private SavedNode GetWorkflowNode(Guid workflowId, Guid workflowStepId)
        {
            using (var cxt = _dbFactory.CreateDbContext())
            {
                var wfStep = cxt.Workflows.FirstOrDefault(w => w.WorkflowId == workflowId);
                if (wfStep == null) throw new KeyNotFoundException("workflowId");

                var node = wfStep.SavedData.Nodes.FirstOrDefault(n => n.WorkflowStepId == workflowStepId);
                if (node == null) throw new KeyNotFoundException("workflowStepId");

                return node;
            }
        }
    }
}
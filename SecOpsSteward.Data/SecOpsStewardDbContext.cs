using Microsoft.EntityFrameworkCore;
using SecOpsSteward.Data.Models;

namespace SecOpsSteward.Data
{
    public class SecOpsStewardDbContext : DbContext
    {
        public SecOpsStewardDbContext(DbContextOptions<SecOpsStewardDbContext> options) : base(options)
        {
            try
            {
                // TODO: Migrations
                Database.EnsureCreated();
                IsReady = true;
            }
            catch
            {
            }
        }

        public DbSet<AgentModel> Agents { get; set; }
        public DbSet<AgentGrantModel> AgentGrants { get; set; }
        public DbSet<AgentPermissionModel> AgentPermissions { get; set; }

        public DbSet<ContainerModel> Containers { get; set; }
        public DbSet<ManagedServiceModel> ManagedServices { get; set; }
        public DbSet<PluginMetadataModel> Plugins { get; set; }

        public DbSet<UserModel> Users { get; set; }
        public DbSet<WorkflowModel> Workflows { get; set; }
        public DbSet<WorkflowRecurrenceModel> WorkflowRecurrences { get; set; }
        public DbSet<WorkflowExecutionModel> WorkflowExecutions { get; set; }
        public DbSet<WorkflowTemplateModel> WorkflowTemplates { get; set; }
        public DbSet<WorkflowTemplateParticipantModel> WorkflowTemplateParticipants { get; set; }

        public bool IsReady { get; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // add complex keys here
            modelBuilder.Entity<AgentPermissionModel>()
                .HasKey(apm => new {apm.AgentId, apm.UserId, apm.PackageId});

            // ---

            modelBuilder.Entity<AgentModel>()
                .HasMany(a => a.Permissions)
                .WithOne(p => p.Agent)
                .HasForeignKey(p => p.AgentId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // ---

            modelBuilder.Entity<AgentGrantModel>()
                .HasOne(agm => agm.UserPerformingGrant)
                .WithMany(um => um.AgentPackageGrants)
                .HasForeignKey(agm => agm.UserPerformingGrantId)
                .IsRequired();

            modelBuilder.Entity<AgentGrantModel>()
                .HasOne(agm => agm.Plugin)
                .WithMany(pm => pm.AgentPackageGrants)
                .HasForeignKey(agm => agm.PluginId)
                .IsRequired();

            modelBuilder.Entity<AgentGrantModel>()
                .HasOne(agm => agm.Agent)
                .WithMany(am => am.AgentPackageGrants)
                .HasForeignKey(agm => agm.AgentId)
                .IsRequired();

            // ---

            modelBuilder.Entity<WorkflowModel>()
                .HasOne(e => e.GrantingUser)
                .WithMany()
                .HasForeignKey(e => e.GrantingUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<WorkflowTemplateModel>()
                .HasMany(tm => tm.Participants)
                .WithOne(pm => pm.WorkflowTemplate)
                .HasForeignKey(tm => tm.WorkflowTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkflowExecutionModel>()
                .HasOne(wem => wem.Workflow)
                .WithMany()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<WorkflowRecurrenceModel>()
                .HasOne(wrm => wrm.Workflow)
                .WithMany()
                .HasForeignKey(wrm => wrm.WorkflowId);

            // ---

            modelBuilder.Entity<ManagedServiceModel>()
                .HasMany(msm => msm.Templates)
                .WithOne(tm => tm.ManagedService)
                .HasForeignKey(msm => msm.ManagedServiceId);

            modelBuilder.Entity<ManagedServiceModel>()
                .HasMany(msm => msm.Plugins)
                .WithOne(pm => pm.ManagedService)
                .HasForeignKey(msm => msm.ManagedServiceId);

            // ---

            modelBuilder.Entity<ContainerModel>()
                .HasMany(cm => cm.ManagedServices)
                .WithOne(msm => msm.Container)
                .HasForeignKey(cm => cm.ContainerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PluginMetadataModel>()
                .HasMany(pmm => pmm.Permissions)
                .WithOne(pm => pm.Package)
                .HasForeignKey(pmm => pmm.PackageId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // ---

            modelBuilder.Entity<UserModel>()
                .HasMany(um => um.Permissions)
                .WithOne(pm => pm.User)
                .OnDelete(DeleteBehavior.Cascade)
                .HasForeignKey(um => um.UserId);
        }
    }
}
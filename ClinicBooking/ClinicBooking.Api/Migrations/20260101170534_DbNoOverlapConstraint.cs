using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicBooking.Api.Migrations
{
    public partial class DbNoOverlapConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- Nettoyage SAFE (si déjà supprimé, pas d'erreur)
ALTER TABLE ""Appointments"" DROP CONSTRAINT IF EXISTS ""FK_Appointments_Patients_PatientId1"";
ALTER TABLE ""Appointments"" DROP CONSTRAINT IF EXISTS ""FK_Appointments_Practitioners_PractitionerId1"";

DROP INDEX IF EXISTS ""IX_Appointments_PatientId1"";
DROP INDEX IF EXISTS ""IX_Appointments_PractitionerId1"";

ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""PatientId1"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""PractitionerId1"";

-- Recrée proprement la règle anti-chevauchement (Scheduled seulement)
ALTER TABLE ""Appointments"" DROP CONSTRAINT IF EXISTS ""EX_Appointments_NoOverlap"";

ALTER TABLE ""Appointments""
  ADD CONSTRAINT ""EX_Appointments_NoOverlap""
  EXCLUDE USING gist (
    ""PractitionerId"" WITH =,
    tstzrange(""StartUtc"", ""EndUtc"", '[)') WITH &&
  )
  WHERE (""Status"" = 'Scheduled');
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down “safe” (on évite de réintroduire les shadow cols/FKs)
            migrationBuilder.Sql(@"
ALTER TABLE ""Appointments"" DROP CONSTRAINT IF EXISTS ""EX_Appointments_NoOverlap"";
");
        }
    }
}

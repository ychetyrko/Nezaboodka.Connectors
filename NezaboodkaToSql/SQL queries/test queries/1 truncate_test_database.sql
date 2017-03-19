/*********************************************

			Truncate nz_test_db

**********************************************/

USE `nz_admin_db`;

CALL before_alter_database_schema();
INSERT INTO `type_rem_list`
(`name`)
SELECT `name`
FROM `nz_test_db`.`type`;
CALL alter_database_schema('nz_test_db');

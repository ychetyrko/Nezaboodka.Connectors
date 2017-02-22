/*======================================
		Truncate database
======================================*/

USE `nz_test_closure`;

CALL before_alter_db_schema();
INSERT INTO `type_rem_list`
(`name`)
SELECT `name`
FROM `nz_test_closure`.`type`;
CALL alter_db_schema('nz_test_closure');

/*********************************************

	Drop Nezaboodka administrative database
		with all Nezaboodka databases
			and users
        
**********************************************/

USE `nz_admin_db`;

CALL before_alter_database_list();
INSERT INTO `db_rem_list` (
	SELECT `name` FROM `db_list`
);
CALL alter_database_list();
CALL cleanup_removed_databases();


DROP DATABASE `nz_admin_db`;

DROP USER `nz_admin`@'localhost';
DROP USER `nz_admin`@'%';
FLUSH PRIVILEGES;

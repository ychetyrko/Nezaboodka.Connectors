/*********************************************

	Drop Nezaboodka administrative database
		with all Nezaboodka databases
			and users
        
**********************************************/

USE `nz_admin_db`;
INSERT INTO `db_rem_list` (
	SELECT `name` FROM `db_list`
);
CALL alter_database_list();

DROP DATABASE `nz_admin_db`;

DELETE FROM mysql.user WHERE User = 'nz_admin';
FLUSH PRIVILEGES;
